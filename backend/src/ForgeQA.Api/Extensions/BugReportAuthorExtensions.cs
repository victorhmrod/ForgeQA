using System.Security.Claims;
using ForgeQA.Api.Auth;
using ForgeQA.Application.Bugs;
using ForgeQA.Domain.Enums;

namespace ForgeQA.Api.Extensions;

public static class BugReportAuthorExtensions
{
    /// <summary>
    /// Builds the caller identity for a dual-scheme (user JWT or Project API key) endpoint from
    /// whichever scheme actually authenticated the request — never both.
    /// </summary>
    public static BugReportAuthor ToBugReportAuthor(this ClaimsPrincipal user)
    {
        var apiKeyProjectIdClaim = user.FindFirst(ProjectApiKeyDefaults.ProjectApiKeyProjectIdClaim);
        var apiKeyIdClaim = user.FindFirst(ProjectApiKeyDefaults.ProjectApiKeyIdClaim);
        if (apiKeyIdClaim is not null && apiKeyProjectIdClaim is not null)
        {
            var scopes = user.FindAll(ProjectApiKeyDefaults.ProjectApiKeyScopeClaim)
                .Select(c => Enum.Parse<ProjectApiKeyScope>(c.Value))
                .ToList();
            return BugReportAuthor.FromApiKey(Guid.Parse(apiKeyIdClaim.Value), Guid.Parse(apiKeyProjectIdClaim.Value), scopes);
        }

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        return BugReportAuthor.FromUser(Guid.Parse(userId!));
    }
}
