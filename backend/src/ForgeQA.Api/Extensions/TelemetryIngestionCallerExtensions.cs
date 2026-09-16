using System.Security.Claims;
using ForgeQA.Api.Auth;
using ForgeQA.Application.Telemetry;
using ForgeQA.Domain.Enums;

namespace ForgeQA.Api.Extensions;

public static class TelemetryIngestionCallerExtensions
{
    /// <summary>
    /// Builds the caller for a telemetry-ingestion endpoint, which authenticates exclusively via
    /// the ForgeQAProjectKey scheme (see docs/telemetry.md — telemetry ingestion is machine-oriented
    /// only, unlike Bug Reporting's dual-scheme endpoints) — so the API key claims are always present.
    /// </summary>
    public static TelemetryIngestionCaller ToTelemetryIngestionCaller(this ClaimsPrincipal user)
    {
        var apiKeyId = Guid.Parse(user.FindFirst(ProjectApiKeyDefaults.ProjectApiKeyIdClaim)!.Value);
        var projectId = Guid.Parse(user.FindFirst(ProjectApiKeyDefaults.ProjectApiKeyProjectIdClaim)!.Value);
        var scopes = user.FindAll(ProjectApiKeyDefaults.ProjectApiKeyScopeClaim)
            .Select(c => Enum.Parse<ProjectApiKeyScope>(c.Value))
            .ToList();

        return new TelemetryIngestionCaller(apiKeyId, projectId, scopes);
    }
}
