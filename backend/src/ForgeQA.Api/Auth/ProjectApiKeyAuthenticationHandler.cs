using System.Security.Claims;
using System.Text.Encodings.Web;
using ForgeQA.Application.Abstractions;
using ForgeQA.Application.ProjectApiKeys;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ForgeQA.Api.Auth;

public static class ProjectApiKeyDefaults
{
    public const string AuthenticationScheme = "ForgeQAProjectKey";
    public const string HeaderName = "X-ForgeQA-Key";

    public const string ProjectApiKeyIdClaim = "forgeqa:api_key_id";
    public const string ProjectApiKeyProjectIdClaim = "forgeqa:api_key_project_id";
    public const string ProjectApiKeyScopeClaim = "forgeqa:api_key_scope";
}

/// <summary>
/// Authenticates runtime submissions (e.g. the Unreal plugin) using a Project-scoped API key sent
/// in the <c>X-ForgeQA-Key</c> header — deliberately not <c>Authorization: Bearer</c>, so it can
/// never be confused with (or accidentally parsed as) a user JWT when both schemes are combined
/// on one endpoint. See docs/bug-reporting.md for the full security model. This never accepts a
/// user session; it authenticates a machine credential for exactly one Project.
/// </summary>
public class ProjectApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IProjectApiKeyRepository _apiKeyRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProjectApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IProjectApiKeyRepository apiKeyRepository,
        IUnitOfWork unitOfWork)
        : base(options, logger, encoder)
    {
        _apiKeyRepository = apiKeyRepository;
        _unitOfWork = unitOfWork;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ProjectApiKeyDefaults.HeaderName, out var headerValues))
            return AuthenticateResult.NoResult();

        var candidate = headerValues.ToString();
        if (!ProjectApiKeyHasher.LooksLikeProjectApiKey(candidate))
            return AuthenticateResult.Fail("Malformed ForgeQA API key.");

        var hash = ProjectApiKeyHasher.Hash(candidate);
        var apiKey = await _apiKeyRepository.GetByHashAsync(hash, Context.RequestAborted);

        // Deliberately identical failure for "not found" and "revoked" — never let an attacker
        // distinguish a valid-but-revoked key from one that never existed.
        if (apiKey is null || !apiKey.IsActive)
            return AuthenticateResult.Fail("Invalid or revoked ForgeQA API key.");

        apiKey.MarkUsed();
        await _unitOfWork.SaveChangesAsync(Context.RequestAborted);

        var claims = new List<Claim>
        {
            new(ProjectApiKeyDefaults.ProjectApiKeyIdClaim, apiKey.Id.ToString()),
            new(ProjectApiKeyDefaults.ProjectApiKeyProjectIdClaim, apiKey.ProjectId.ToString()),
        };
        claims.AddRange(apiKey.Scopes.Select(scope => new Claim(ProjectApiKeyDefaults.ProjectApiKeyScopeClaim, scope.ToString())));

        var identity = new ClaimsIdentity(claims, ProjectApiKeyDefaults.AuthenticationScheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), ProjectApiKeyDefaults.AuthenticationScheme);
        return AuthenticateResult.Success(ticket);
    }
}
