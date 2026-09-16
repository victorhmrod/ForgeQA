using ForgeQA.Application.Abstractions;
using ForgeQA.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ForgeQA.Infrastructure.Persistence.Repositories;

public class TelemetryRepository : ITelemetryRepository
{
    private readonly ForgeQADbContext _dbContext;

    public TelemetryRepository(ForgeQADbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<TelemetrySession?> GetByRuntimeSessionIdAsync(Guid projectId, Guid runtimeSessionId, CancellationToken cancellationToken) =>
        _dbContext.TelemetrySessions.FirstOrDefaultAsync(s => s.ProjectId == projectId && s.RuntimeSessionId == runtimeSessionId, cancellationToken);

    public Task<TelemetrySession?> GetByIdForProjectAsync(Guid projectId, Guid sessionId, CancellationToken cancellationToken) =>
        _dbContext.TelemetrySessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.ProjectId == projectId, cancellationToken);

    public void Add(TelemetrySession session) => _dbContext.TelemetrySessions.Add(session);

    public Task<int> GetEventCountAsync(Guid telemetrySessionId, CancellationToken cancellationToken) =>
        _dbContext.TelemetryEvents.CountAsync(e => e.TelemetrySessionId == telemetrySessionId, cancellationToken);

    public async Task<(IReadOnlyList<TelemetrySessionQueryItem> Items, int TotalCount)> QueryAsync(TelemetrySessionQuery query, CancellationToken cancellationToken)
    {
        var sessions = _dbContext.TelemetrySessions.AsNoTracking().Where(s => s.ProjectId == query.ProjectId);

        if (query.BuildId is not null)
            sessions = sessions.Where(s => s.BuildId == query.BuildId);

        if (query.From is not null)
            sessions = sessions.Where(s => s.StartedAt >= query.From);

        if (query.To is not null)
            sessions = sessions.Where(s => s.StartedAt <= query.To);

        if (query.RuntimeSessionId is not null)
            sessions = sessions.Where(s => s.RuntimeSessionId == query.RuntimeSessionId);

        if (query.ActiveOnly is true)
            sessions = sessions.Where(s => s.EndedAt == null);
        else if (query.ActiveOnly is false)
            sessions = sessions.Where(s => s.EndedAt != null);

        var totalCount = await sessions.CountAsync(cancellationToken);

        // A correlated scalar subquery for the event count avoids loading each session's full
        // Events collection just to measure it — no N+1, no client-side aggregation.
        var items = await sessions
            .OrderByDescending(s => s.StartedAt)
            .ThenByDescending(s => s.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(s => new { Session = s, EventCount = _dbContext.TelemetryEvents.Count(e => e.TelemetrySessionId == s.Id) })
            .ToListAsync(cancellationToken);

        return (items.Select(i => new TelemetrySessionQueryItem(i.Session, i.EventCount)).ToList(), totalCount);
    }

    public async Task<HashSet<int>> GetExistingSequenceNumbersAsync(Guid telemetrySessionId, IReadOnlyCollection<int> sequenceNumbers, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.TelemetryEvents
            .Where(e => e.TelemetrySessionId == telemetrySessionId && sequenceNumbers.Contains(e.SequenceNumber))
            .Select(e => e.SequenceNumber)
            .ToListAsync(cancellationToken);

        return existing.ToHashSet();
    }

    public void AddEvents(IEnumerable<TelemetryEvent> events) => _dbContext.TelemetryEvents.AddRange(events);

    public async Task<(IReadOnlyList<TelemetryEvent> Items, int TotalCount)> QueryEventsAsync(
        Guid telemetrySessionId, int page, int pageSize, string? eventNamePrefix, CancellationToken cancellationToken)
    {
        var events = _dbContext.TelemetryEvents.AsNoTracking().Where(e => e.TelemetrySessionId == telemetrySessionId);

        if (!string.IsNullOrWhiteSpace(eventNamePrefix))
            events = events.Where(e => e.EventName.StartsWith(eventNamePrefix));

        var totalCount = await events.CountAsync(cancellationToken);

        var items = await events
            .OrderBy(e => e.SequenceNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
