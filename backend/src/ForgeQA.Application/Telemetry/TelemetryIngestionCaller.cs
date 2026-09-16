using ForgeQA.Domain.Enums;

namespace ForgeQA.Application.Telemetry;

/// <summary>
/// The Project API key making a telemetry ingestion call. Unlike Bug Reporting, telemetry
/// ingestion is machine-oriented only — there is no dashboard-user equivalent — so this is always
/// an API key, never a JWT user. See docs/telemetry.md.
/// </summary>
public sealed class TelemetryIngestionCaller
{
    public Guid ApiKeyId { get; }
    public Guid ProjectId { get; }
    private readonly IReadOnlyCollection<ProjectApiKeyScope> _scopes;

    public TelemetryIngestionCaller(Guid apiKeyId, Guid projectId, IReadOnlyCollection<ProjectApiKeyScope> scopes)
    {
        ApiKeyId = apiKeyId;
        ProjectId = projectId;
        _scopes = scopes;
    }

    public bool HasScope(ProjectApiKeyScope scope) => _scopes.Contains(scope);
}
