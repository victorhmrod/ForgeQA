using ForgeQA.Domain.Entities;

namespace ForgeQA.Application.Abstractions;

public record TelemetrySessionQuery(
    Guid ProjectId,
    int Page,
    int PageSize,
    Guid? BuildId,
    DateTime? From,
    DateTime? To,
    Guid? RuntimeSessionId,
    bool? ActiveOnly);

public record TelemetrySessionQueryItem(TelemetrySession Session, int EventCount);

public interface ITelemetryRepository
{
    Task<TelemetrySession?> GetByRuntimeSessionIdAsync(Guid projectId, Guid runtimeSessionId, CancellationToken cancellationToken);
    Task<TelemetrySession?> GetByIdForProjectAsync(Guid projectId, Guid sessionId, CancellationToken cancellationToken);
    void Add(TelemetrySession session);

    Task<int> GetEventCountAsync(Guid telemetrySessionId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<TelemetrySessionQueryItem> Items, int TotalCount)> QueryAsync(TelemetrySessionQuery query, CancellationToken cancellationToken);

    /// <summary>Returns which of the given sequence numbers already exist for this session, in one
    /// query — used to compute batch accepted/duplicate counts without inserting one row at a time.</summary>
    Task<HashSet<int>> GetExistingSequenceNumbersAsync(Guid telemetrySessionId, IReadOnlyCollection<int> sequenceNumbers, CancellationToken cancellationToken);
    void AddEvents(IEnumerable<TelemetryEvent> events);

    Task<(IReadOnlyList<TelemetryEvent> Items, int TotalCount)> QueryEventsAsync(
        Guid telemetrySessionId, int page, int pageSize, string? eventNamePrefix, CancellationToken cancellationToken);
}
