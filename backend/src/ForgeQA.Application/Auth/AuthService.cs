using ForgeQA.Application.Abstractions;
using ForgeQA.Application.Common;
using ForgeQA.Domain.Common;
using ForgeQA.Domain.Entities;
using ForgeQA.Domain.Enums;

namespace ForgeQA.Application.Auth;

public class AuthService
{
    private readonly IIdentityService _identityService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AuthService(IIdentityService identityService, IOrganizationRepository organizationRepository, IUnitOfWork unitOfWork)
    {
        _identityService = identityService;
        _organizationRepository = organizationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var (result, user, errors) = await _identityService.RegisterAsync(request.Email, request.DisplayName, request.Password, cancellationToken);

        if (result == RegisterResult.EmailAlreadyExists)
            return Result<AuthResponse>.Failure(ErrorType.Conflict, "A user with this email already exists.");

        if (result != RegisterResult.Success || user is null)
            return Result<AuthResponse>.Failure(ErrorType.Validation, string.Join(" ", errors));

        var baseSlug = SlugGenerator.Generate($"{request.DisplayName}s workspace");
        var slug = await EnsureUniqueSlugAsync(baseSlug, cancellationToken);

        var organization = new Organization($"{request.DisplayName}'s Workspace", slug);
        organization.AddMember(user.Id, OrganizationRole.Owner);
        _organizationRepository.Add(organization);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var tokens = await _identityService.IssueTokensAsync(user, cancellationToken);
        return Result<AuthResponse>.Success(ToAuthResponse(tokens, user));
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var (result, user) = await _identityService.ValidateCredentialsAsync(request.Email, request.Password, cancellationToken);

        if (result != LoginResult.Success || user is null)
            return Result<AuthResponse>.Failure(ErrorType.Unauthorized, "Invalid email or password.");

        var tokens = await _identityService.IssueTokensAsync(user, cancellationToken);
        return Result<AuthResponse>.Success(ToAuthResponse(tokens, user));
    }

    public async Task<Result<AuthResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        var (result, user, tokens) = await _identityService.RefreshTokensAsync(request.RefreshToken, cancellationToken);

        if (result != RefreshResult.Success || user is null || tokens is null)
            return Result<AuthResponse>.Failure(ErrorType.Unauthorized, "Invalid or expired refresh token.");

        return Result<AuthResponse>.Success(ToAuthResponse(tokens, user));
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        await _identityService.RevokeRefreshTokenAsync(refreshToken, cancellationToken);
    }

    public async Task<Result<CurrentUserResponse>> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _identityService.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
            return Result<CurrentUserResponse>.Failure(ErrorType.NotFound, "User not found.");

        return Result<CurrentUserResponse>.Success(new CurrentUserResponse(user.Id, user.Email, user.DisplayName, user.CreatedAt));
    }

    private async Task<string> EnsureUniqueSlugAsync(string baseSlug, CancellationToken cancellationToken)
    {
        var slug = baseSlug;
        var suffix = 1;
        while (await _organizationRepository.SlugExistsAsync(slug, cancellationToken))
        {
            suffix++;
            slug = $"{baseSlug}-{suffix}";
        }
        return slug;
    }

    private static AuthResponse ToAuthResponse(AuthTokens tokens, IdentityUserInfo user) => new(
        tokens.AccessToken,
        tokens.AccessTokenExpiresAt,
        tokens.RefreshToken,
        tokens.RefreshTokenExpiresAt,
        new CurrentUserResponse(user.Id, user.Email, user.DisplayName, user.CreatedAt));
}
