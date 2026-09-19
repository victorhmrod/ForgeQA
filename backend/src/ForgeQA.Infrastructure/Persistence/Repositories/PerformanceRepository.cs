using ForgeQA.Application.Abstractions;
using ForgeQA.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ForgeQA.Infrastructure.Persistence.Repositories;

public class PerformanceRepository : IPerformanceRepository
{
    private readonly ForgeQADbContext _dbContext;

    public PerformanceRepository(ForgeQADbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HashSet<int>> GetExistingSequenceNumbersAsync(Guid telemetrySessionId, IReadOnlyCollection<int> sequenceNumbers, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.PerformanceSamples
            .Where(s => s.TelemetrySessionId == telemetrySessionId && sequenceNumbers.Contains(s.SequenceNumber))
            .Select(s => s.SequenceNumber)
            .ToListAsync(cancellationToken);

        return existing.ToHashSet();
    }

    public void AddSamples(IEnumerable<PerformanceSample> samples) => _dbContext.PerformanceSamples.AddRange(samples);

    public async Task<(IReadOnlyList<PerformanceSample> Items, int TotalCount)> QuerySamplesAsync(Guid telemetrySessionId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var samples = _dbContext.PerformanceSamples.AsNoTracking().Where(s => s.TelemetrySessionId == telemetrySessionId);

        var totalCount = await samples.CountAsync(cancellationToken);

        var items = await samples
            .OrderBy(s => s.SequenceNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<PerformanceSummary> GetSummaryAsync(Guid telemetrySessionId, CancellationToken cancellationToken)
    {
        var rows = await _dbContext.Database.SqlQuery<PerformanceSummaryRow>($"""
            SELECT
                COUNT(*)::int AS "SampleCount",
                AVG("Fps") AS "AverageFps",
                MIN("Fps") AS "MinimumFps",
                AVG("FrameTimeMs") AS "AverageFrameTimeMs",
                PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY "FrameTimeMs") AS "P50FrameTimeMs",
                PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY "FrameTimeMs") AS "P95FrameTimeMs",
                PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY "FrameTimeMs") AS "P99FrameTimeMs",
                MAX("FrameTimeMs") AS "MaximumFrameTimeMs",
                AVG("MemoryUsedBytes"::double precision) AS "AverageMemoryUsedBytes",
                MAX("MemoryUsedBytes") AS "PeakMemoryUsedBytes",
                AVG("GameThreadTimeMs") AS "AverageGameThreadTimeMs",
                AVG("RenderThreadTimeMs") AS "AverageRenderThreadTimeMs",
                AVG("GpuTimeMs") AS "AverageGpuTimeMs"
            FROM "PerformanceSamples"
            WHERE "TelemetrySessionId" = {telemetrySessionId}
            """).ToListAsync(cancellationToken);

        return ToSummary(rows.Single());
    }

    public async Task<Dictionary<Guid, PerformanceSummary>> GetSummariesForSessionsAsync(IReadOnlyCollection<Guid> telemetrySessionIds, CancellationToken cancellationToken)
    {
        if (telemetrySessionIds.Count == 0)
            return new Dictionary<Guid, PerformanceSummary>();

        var rows = await _dbContext.Database.SqlQuery<PerformanceSessionSummaryRow>($"""
            SELECT
                "TelemetrySessionId" AS "TelemetrySessionId",
                COUNT(*)::int AS "SampleCount",
                AVG("Fps") AS "AverageFps",
                MIN("Fps") AS "MinimumFps",
                AVG("FrameTimeMs") AS "AverageFrameTimeMs",
                PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY "FrameTimeMs") AS "P50FrameTimeMs",
                PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY "FrameTimeMs") AS "P95FrameTimeMs",
                PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY "FrameTimeMs") AS "P99FrameTimeMs",
                MAX("FrameTimeMs") AS "MaximumFrameTimeMs",
                AVG("MemoryUsedBytes"::double precision) AS "AverageMemoryUsedBytes",
                MAX("MemoryUsedBytes") AS "PeakMemoryUsedBytes",
                AVG("GameThreadTimeMs") AS "AverageGameThreadTimeMs",
                AVG("RenderThreadTimeMs") AS "AverageRenderThreadTimeMs",
                AVG("GpuTimeMs") AS "AverageGpuTimeMs"
            FROM "PerformanceSamples"
            WHERE "TelemetrySessionId" = ANY({telemetrySessionIds.ToArray()})
            GROUP BY "TelemetrySessionId"
            """).ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.TelemetrySessionId, ToSummary);
    }

    public async Task<(IReadOnlyList<TelemetrySession> Items, int TotalCount)> QuerySessionsWithSamplesAsync(PerformanceSessionQuery query, CancellationToken cancellationToken)
    {
        var sessions = _dbContext.TelemetrySessions.AsNoTracking()
            .Where(s => s.ProjectId == query.ProjectId)
            .Where(s => _dbContext.PerformanceSamples.Any(p => p.TelemetrySessionId == s.Id));

        if (query.BuildId is not null)
            sessions = sessions.Where(s => s.BuildId == query.BuildId);

        if (query.From is not null)
            sessions = sessions.Where(s => s.StartedAt >= query.From);

        if (query.To is not null)
            sessions = sessions.Where(s => s.StartedAt <= query.To);

        if (!string.IsNullOrWhiteSpace(query.Platform))
            sessions = sessions.Where(s => s.Environment.Platform == query.Platform);

        var totalCount = await sessions.CountAsync(cancellationToken);

        var items = await sessions
            .OrderByDescending(s => s.StartedAt)
            .ThenByDescending(s => s.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<MapBreakdownItem>> GetMapBreakdownAsync(Guid telemetrySessionId, CancellationToken cancellationToken)
    {
        var rows = await _dbContext.Database.SqlQuery<MapBreakdownRow>($"""
            SELECT
                COALESCE("MapName", '(unknown)') AS "MapName",
                COUNT(*)::int AS "SampleCount",
                AVG("Fps") AS "AverageFps",
                PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY "FrameTimeMs") AS "P95FrameTimeMs"
            FROM "PerformanceSamples"
            WHERE "TelemetrySessionId" = {telemetrySessionId}
            GROUP BY COALESCE("MapName", '(unknown)')
            ORDER BY COUNT(*) DESC
            """).ToListAsync(cancellationToken);

        return rows.Select(r => new MapBreakdownItem(r.MapName, r.SampleCount, r.AverageFps, r.P95FrameTimeMs)).ToList();
    }

    public async Task<List<BuildPerformanceSummary>> GetBuildSummariesAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var rows = await _dbContext.Database.SqlQuery<BuildPerformanceSummaryRow>($"""
            SELECT
                ts."BuildId" AS "BuildId",
                COUNT(DISTINCT ts."Id")::int AS "SessionCount",
                COUNT(ps."Id")::int AS "SampleCount",
                AVG(ps."Fps") AS "AverageFps",
                MIN(ps."Fps") AS "MinimumFps",
                AVG(ps."FrameTimeMs") AS "AverageFrameTimeMs",
                PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY ps."FrameTimeMs") AS "P50FrameTimeMs",
                PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY ps."FrameTimeMs") AS "P95FrameTimeMs",
                PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY ps."FrameTimeMs") AS "P99FrameTimeMs",
                MAX(ps."FrameTimeMs") AS "MaximumFrameTimeMs",
                AVG(ps."MemoryUsedBytes"::double precision) AS "AverageMemoryUsedBytes",
                MAX(ps."MemoryUsedBytes") AS "PeakMemoryUsedBytes",
                AVG(ps."GameThreadTimeMs") AS "AverageGameThreadTimeMs",
                AVG(ps."RenderThreadTimeMs") AS "AverageRenderThreadTimeMs",
                AVG(ps."GpuTimeMs") AS "AverageGpuTimeMs"
            FROM "PerformanceSamples" ps
            JOIN "TelemetrySessions" ts ON ts."Id" = ps."TelemetrySessionId"
            WHERE ts."ProjectId" = {projectId}
            GROUP BY ts."BuildId"
            """).ToListAsync(cancellationToken);

        return rows.Select(r => new BuildPerformanceSummary(r.BuildId, r.SessionCount, ToSummary(r))).ToList();
    }

    public async Task<PerformanceSummary?> GetBuildSummaryAsync(Guid projectId, Guid buildId, CancellationToken cancellationToken)
    {
        var rows = await _dbContext.Database.SqlQuery<PerformanceSummaryRow>($"""
            SELECT
                COUNT(ps."Id")::int AS "SampleCount",
                AVG(ps."Fps") AS "AverageFps",
                MIN(ps."Fps") AS "MinimumFps",
                AVG(ps."FrameTimeMs") AS "AverageFrameTimeMs",
                PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY ps."FrameTimeMs") AS "P50FrameTimeMs",
                PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY ps."FrameTimeMs") AS "P95FrameTimeMs",
                PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY ps."FrameTimeMs") AS "P99FrameTimeMs",
                MAX(ps."FrameTimeMs") AS "MaximumFrameTimeMs",
                AVG(ps."MemoryUsedBytes"::double precision) AS "AverageMemoryUsedBytes",
                MAX(ps."MemoryUsedBytes") AS "PeakMemoryUsedBytes",
                AVG(ps."GameThreadTimeMs") AS "AverageGameThreadTimeMs",
                AVG(ps."RenderThreadTimeMs") AS "AverageRenderThreadTimeMs",
                AVG(ps."GpuTimeMs") AS "AverageGpuTimeMs"
            FROM "TelemetrySessions" ts
            LEFT JOIN "PerformanceSamples" ps ON ps."TelemetrySessionId" = ts."Id"
            WHERE ts."ProjectId" = {projectId} AND ts."BuildId" = {buildId}
            """).ToListAsync(cancellationToken);

        return ToSummary(rows.Single());
    }

    public async Task<List<PerformanceSeriesPoint>> GetSeriesAsync(Guid telemetrySessionId, int maxPoints, CancellationToken cancellationToken)
    {
        var sampleCount = await _dbContext.PerformanceSamples.CountAsync(s => s.TelemetrySessionId == telemetrySessionId, cancellationToken);
        if (sampleCount == 0)
            return new List<PerformanceSeriesPoint>();

        var bucketSize = Math.Max(1, (int)Math.Ceiling(sampleCount / (double)maxPoints));

        var rows = await _dbContext.Database.SqlQuery<PerformanceSeriesRow>($"""
            WITH numbered AS (
                SELECT
                    "ClientTimestamp", "FrameTimeMs", "Fps", "MemoryUsedBytes",
                    (ROW_NUMBER() OVER (ORDER BY "SequenceNumber") - 1) / {bucketSize} AS "Bucket"
                FROM "PerformanceSamples"
                WHERE "TelemetrySessionId" = {telemetrySessionId}
            )
            SELECT
                MIN("ClientTimestamp") AS "Timestamp",
                AVG("FrameTimeMs") AS "AverageFrameTimeMs",
                MAX("FrameTimeMs") AS "MaximumFrameTimeMs",
                AVG("Fps") AS "AverageFps",
                MIN("Fps") AS "MinimumFps",
                AVG("MemoryUsedBytes"::double precision)::bigint AS "AverageMemoryUsedBytes"
            FROM numbered
            GROUP BY "Bucket"
            ORDER BY "Bucket"
            """).ToListAsync(cancellationToken);

        return rows.Select(r => new PerformanceSeriesPoint(
            r.Timestamp, r.AverageFrameTimeMs, r.MaximumFrameTimeMs, r.AverageFps, r.MinimumFps, r.AverageMemoryUsedBytes)).ToList();
    }

    private static PerformanceSummary ToSummary(PerformanceSummaryRow row) => new(
        row.SampleCount, row.AverageFps, row.MinimumFps, row.AverageFrameTimeMs, row.P50FrameTimeMs, row.P95FrameTimeMs,
        row.P99FrameTimeMs, row.MaximumFrameTimeMs, row.AverageMemoryUsedBytes, row.PeakMemoryUsedBytes,
        row.AverageGameThreadTimeMs, row.AverageRenderThreadTimeMs, row.AverageGpuTimeMs);

    // Plain mutable classes for raw-SQL projection materialization — EF Core's ad-hoc SqlQuery<T>
    // needs a parameterless constructor and settable properties, so these are intentionally not
    // the public record DTOs (which are immutable/positional).
    private class PerformanceSummaryRow
    {
        public int SampleCount { get; set; }
        public double? AverageFps { get; set; }
        public double? MinimumFps { get; set; }
        public double? AverageFrameTimeMs { get; set; }
        public double? P50FrameTimeMs { get; set; }
        public double? P95FrameTimeMs { get; set; }
        public double? P99FrameTimeMs { get; set; }
        public double? MaximumFrameTimeMs { get; set; }
        public double? AverageMemoryUsedBytes { get; set; }
        public long? PeakMemoryUsedBytes { get; set; }
        public double? AverageGameThreadTimeMs { get; set; }
        public double? AverageRenderThreadTimeMs { get; set; }
        public double? AverageGpuTimeMs { get; set; }
    }

    private class PerformanceSessionSummaryRow : PerformanceSummaryRow
    {
        public Guid TelemetrySessionId { get; set; }
    }

    private class BuildPerformanceSummaryRow : PerformanceSummaryRow
    {
        public Guid BuildId { get; set; }
        public int SessionCount { get; set; }
    }

    private class MapBreakdownRow
    {
        public string MapName { get; set; } = string.Empty;
        public int SampleCount { get; set; }
        public double? AverageFps { get; set; }
        public double? P95FrameTimeMs { get; set; }
    }

    private class PerformanceSeriesRow
    {
        public DateTime Timestamp { get; set; }
        public double? AverageFrameTimeMs { get; set; }
        public double? MaximumFrameTimeMs { get; set; }
        public double? AverageFps { get; set; }
        public double? MinimumFps { get; set; }
        public long? AverageMemoryUsedBytes { get; set; }
    }
}
