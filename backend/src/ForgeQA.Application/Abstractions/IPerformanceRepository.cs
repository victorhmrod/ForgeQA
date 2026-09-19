using ForgeQA.Domain.Entities;

namespace ForgeQA.Application.Abstractions;

/// <summary>Nullable-safe aggregate: every metric is null when the underlying sample set has no
/// data for it (e.g. GPU timing unavailable on the collecting platform), never zero.</summary>
public record PerformanceSummary(
    int SampleCount,
    double? AverageFps,
    double? MinimumFps,
    double? AverageFrameTimeMs,
    double? P50FrameTimeMs,
    double? P95FrameTimeMs,
    double? P99FrameTimeMs,
    double? MaximumFrameTimeMs,
    double? AverageMemoryUsedBytes,
    long? PeakMemoryUsedBytes,
    double? AverageGameThreadTimeMs,
    double? AverageRenderThreadTimeMs,
    double? AverageGpuTimeMs);

public record BuildPerformanceSummary(Guid BuildId, int SessionCount, PerformanceSummary Summary);

public record MapBreakdownItem(string MapName, int SampleCount, double? AverageFps, double? P95FrameTimeMs);

public record PerformanceSeriesPoint(
    DateTime Timestamp,
    double? AverageFrameTimeMs,
    double? MaximumFrameTimeMs,
    double? AverageFps,
    double? MinimumFps,
    long? AverageMemoryUsedBytes);

public record PerformanceSessionQuery(
    Guid ProjectId,
    int Page,
    int PageSize,
    Guid? BuildId,
    DateTime? From,
    DateTime? To,
    string? Platform);

public interface IPerformanceRepository
{
    Task<HashSet<int>> GetExistingSequenceNumbersAsync(Guid telemetrySessionId, IReadOnlyCollection<int> sequenceNumbers, CancellationToken cancellationToken);
    void AddSamples(IEnumerable<PerformanceSample> samples);

    Task<(IReadOnlyList<PerformanceSample> Items, int TotalCount)> QuerySamplesAsync(Guid telemetrySessionId, int page, int pageSize, CancellationToken cancellationToken);

    Task<PerformanceSummary> GetSummaryAsync(Guid telemetrySessionId, CancellationToken cancellationToken);

    /// <summary>Batched summary lookup for a page of sessions — one aggregate query for the whole
    /// page, never one query per session.</summary>
    Task<Dictionary<Guid, PerformanceSummary>> GetSummariesForSessionsAsync(IReadOnlyCollection<Guid> telemetrySessionIds, CancellationToken cancellationToken);

    /// <summary>Telemetry session ids (scoped to the given Project, and optionally a Build/date
    /// range/platform) that have at least one performance sample, paginated by most recently
    /// started first.</summary>
    Task<(IReadOnlyList<TelemetrySession> Items, int TotalCount)> QuerySessionsWithSamplesAsync(PerformanceSessionQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<MapBreakdownItem>> GetMapBreakdownAsync(Guid telemetrySessionId, CancellationToken cancellationToken);

    Task<List<BuildPerformanceSummary>> GetBuildSummariesAsync(Guid projectId, CancellationToken cancellationToken);

    Task<PerformanceSummary?> GetBuildSummaryAsync(Guid projectId, Guid buildId, CancellationToken cancellationToken);

    Task<List<PerformanceSeriesPoint>> GetSeriesAsync(Guid telemetrySessionId, int maxPoints, CancellationToken cancellationToken);
}
