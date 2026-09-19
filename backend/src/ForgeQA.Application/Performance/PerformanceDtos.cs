using ForgeQA.Application.Abstractions;
using ForgeQA.Domain.Enums;

namespace ForgeQA.Application.Performance;

public record PerformanceSampleRequest(
    int SequenceNumber,
    DateTime ClientTimestamp,
    string? MapName,
    double Fps,
    double FrameTimeMs,
    double? GameThreadTimeMs,
    double? RenderThreadTimeMs,
    double? GpuTimeMs,
    long? MemoryUsedBytes,
    long? MemoryAvailableBytes,
    double? CpuUtilizationPercent,
    double? GpuUtilizationPercent,
    int? DrawCalls,
    int? PlayerCount,
    double? PingMs);

public record IngestPerformanceSamplesRequest(IReadOnlyList<PerformanceSampleRequest> Samples);
public record IngestPerformanceSamplesResponse(int Accepted, int Duplicates);

public record PerformanceSampleResponse(
    Guid Id,
    int SequenceNumber,
    DateTime ClientTimestamp,
    DateTime ReceivedAt,
    string? MapName,
    double Fps,
    double FrameTimeMs,
    double? GameThreadTimeMs,
    double? RenderThreadTimeMs,
    double? GpuTimeMs,
    long? MemoryUsedBytes,
    long? MemoryAvailableBytes,
    double? CpuUtilizationPercent,
    double? GpuUtilizationPercent,
    int? DrawCalls,
    int? PlayerCount,
    double? PingMs);

public record PerformanceBuildSummaryResponse(Guid Id, string Version, string BuildNumber, BuildPlatform Platform, BuildConfiguration Configuration);

public record PerformanceSessionSummaryResponse(Guid SessionId, Guid RuntimeSessionId, PerformanceBuildSummaryResponse Build, PerformanceSummary Summary);

public record PerformanceSessionListItemResponse(
    Guid SessionId,
    Guid RuntimeSessionId,
    PerformanceBuildSummaryResponse Build,
    DateTime StartedAt,
    DateTime? EndedAt,
    string? Platform,
    PerformanceSummary Summary);

public record ListPerformanceSessionsRequest(int Page, int PageSize, Guid? BuildId, DateTime? From, DateTime? To, string? Platform);

public record PerformanceSeriesResponse(IReadOnlyList<PerformanceSeriesPoint> Points);

public record BuildPerformanceListItemResponse(
    Guid BuildId,
    string Version,
    string BuildNumber,
    BuildPlatform Platform,
    BuildConfiguration Configuration,
    bool IsArchived,
    int SessionCount,
    PerformanceSummary Summary);

public record PerformanceCompareMetric(string Metric, double? BuildAValue, double? BuildBValue, double? Delta, double? DeltaPercent);

public record PerformanceCompareResponse(
    BuildPerformanceListItemResponse BuildA,
    BuildPerformanceListItemResponse BuildB,
    IReadOnlyList<PerformanceCompareMetric> Metrics);
