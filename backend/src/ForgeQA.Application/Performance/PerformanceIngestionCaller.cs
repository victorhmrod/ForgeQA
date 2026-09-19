using ForgeQA.Domain.Enums;

namespace ForgeQA.Application.Performance;

/// <summary>
/// The Project API key making a performance ingestion call. Like Telemetry, performance ingestion
/// is machine-oriented only — there is no dashboard-user equivalent.
/// </summary>
public sealed class PerformanceIngestionCaller
{
    public Guid ApiKeyId { get; }
    public Guid ProjectId { get; }
    private readonly IReadOnlyCollection<ProjectApiKeyScope> _scopes;

    public PerformanceIngestionCaller(Guid apiKeyId, Guid projectId, IReadOnlyCollection<ProjectApiKeyScope> scopes)
    {
        ApiKeyId = apiKeyId;
        ProjectId = projectId;
        _scopes = scopes;
    }

    public bool HasScope(ProjectApiKeyScope scope) => _scopes.Contains(scope);
}
