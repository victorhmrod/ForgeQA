using System.Security.Claims;
using ForgeQA.Api.Auth;
using ForgeQA.Application.Performance;
using ForgeQA.Domain.Enums;

namespace ForgeQA.Api.Extensions;

public static class PerformanceIngestionCallerExtensions
{
    /// <summary>Builds the caller for a performance-ingestion endpoint, which authenticates
    /// exclusively via the ForgeQAProjectKey scheme (machine-oriented only, same as Telemetry).</summary>
    public static PerformanceIngestionCaller ToPerformanceIngestionCaller(this ClaimsPrincipal user)
    {
        var apiKeyId = Guid.Parse(user.FindFirst(ProjectApiKeyDefaults.ProjectApiKeyIdClaim)!.Value);
        var projectId = Guid.Parse(user.FindFirst(ProjectApiKeyDefaults.ProjectApiKeyProjectIdClaim)!.Value);
        var scopes = user.FindAll(ProjectApiKeyDefaults.ProjectApiKeyScopeClaim)
            .Select(c => Enum.Parse<ProjectApiKeyScope>(c.Value))
            .ToList();

        return new PerformanceIngestionCaller(apiKeyId, projectId, scopes);
    }
}
