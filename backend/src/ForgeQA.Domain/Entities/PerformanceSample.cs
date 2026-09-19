using ForgeQA.Domain.Common;

namespace ForgeQA.Domain.Entities;

/// <summary>
/// One lightweight runtime performance snapshot within a <see cref="TelemetrySession"/>, collected
/// over a short sampling interval (not per-frame) by the Unreal performance subsystem. Immutable
/// and append-only, mirroring <see cref="TelemetryEvent"/>'s conventions exactly:
/// <see cref="SequenceNumber"/> is client-assigned and unique within its session (idempotent batch
/// retries), <see cref="ClientTimestamp"/> is never trusted as server truth on its own, and
/// <see cref="ReceivedAt"/> is always recorded independently.
///
/// M6 is not a profiler: only high-value, cheaply-collectible signals are captured. Every metric
/// beyond FPS/FrameTimeMs is optional and nullable — a null means "not available on this platform,"
/// never zero. Never persist NaN/Infinity; the constructor rejects both.
/// </summary>
public class PerformanceSample : Entity
{
    public const int MapNameMaxLength = 200;

    public Guid TelemetrySessionId { get; private set; }
    public int SequenceNumber { get; private set; }

    public DateTime ClientTimestamp { get; private set; }
    public DateTime ReceivedAt { get; private set; }

    public string? MapName { get; private set; }

    public double Fps { get; private set; }
    public double FrameTimeMs { get; private set; }

    public double? GameThreadTimeMs { get; private set; }
    public double? RenderThreadTimeMs { get; private set; }
    public double? GpuTimeMs { get; private set; }

    public long? MemoryUsedBytes { get; private set; }
    public long? MemoryAvailableBytes { get; private set; }

    public double? CpuUtilizationPercent { get; private set; }
    public double? GpuUtilizationPercent { get; private set; }

    public int? DrawCalls { get; private set; }
    public int? PlayerCount { get; private set; }
    public double? PingMs { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private PerformanceSample() { }

    public PerformanceSample(
        Guid telemetrySessionId,
        int sequenceNumber,
        DateTime clientTimestamp,
        double fps,
        double frameTimeMs,
        string? mapName = null,
        double? gameThreadTimeMs = null,
        double? renderThreadTimeMs = null,
        double? gpuTimeMs = null,
        long? memoryUsedBytes = null,
        long? memoryAvailableBytes = null,
        double? cpuUtilizationPercent = null,
        double? gpuUtilizationPercent = null,
        int? drawCalls = null,
        int? playerCount = null,
        double? pingMs = null)
    {
        if (telemetrySessionId == Guid.Empty)
            throw new ArgumentException("TelemetrySessionId is required.", nameof(telemetrySessionId));
        if (sequenceNumber <= 0)
            throw new ArgumentException("SequenceNumber must be a positive integer.", nameof(sequenceNumber));
        if (clientTimestamp == default)
            throw new ArgumentException("ClientTimestamp is required.", nameof(clientTimestamp));

        TelemetrySessionId = telemetrySessionId;
        SequenceNumber = sequenceNumber;
        ClientTimestamp = DateTime.SpecifyKind(clientTimestamp, DateTimeKind.Utc);
        ReceivedAt = DateTime.UtcNow;

        Fps = RequireFiniteNonNegative(fps, nameof(fps));
        FrameTimeMs = RequireFiniteNonNegative(frameTimeMs, nameof(frameTimeMs));

        GameThreadTimeMs = RequireFiniteNonNegativeOrNull(gameThreadTimeMs, nameof(gameThreadTimeMs));
        RenderThreadTimeMs = RequireFiniteNonNegativeOrNull(renderThreadTimeMs, nameof(renderThreadTimeMs));
        GpuTimeMs = RequireFiniteNonNegativeOrNull(gpuTimeMs, nameof(gpuTimeMs));
        PingMs = RequireFiniteNonNegativeOrNull(pingMs, nameof(pingMs));

        CpuUtilizationPercent = RequirePercentOrNull(cpuUtilizationPercent, nameof(cpuUtilizationPercent));
        GpuUtilizationPercent = RequirePercentOrNull(gpuUtilizationPercent, nameof(gpuUtilizationPercent));

        if (memoryUsedBytes is < 0)
            throw new ArgumentException("MemoryUsedBytes cannot be negative.", nameof(memoryUsedBytes));
        if (memoryAvailableBytes is < 0)
            throw new ArgumentException("MemoryAvailableBytes cannot be negative.", nameof(memoryAvailableBytes));
        if (drawCalls is < 0)
            throw new ArgumentException("DrawCalls cannot be negative.", nameof(drawCalls));
        if (playerCount is < 0)
            throw new ArgumentException("PlayerCount cannot be negative.", nameof(playerCount));

        MemoryUsedBytes = memoryUsedBytes;
        MemoryAvailableBytes = memoryAvailableBytes;
        DrawCalls = drawCalls;
        PlayerCount = playerCount;
        MapName = Truncate(mapName, MapNameMaxLength);

        CreatedAt = DateTime.UtcNow;
    }

    private static double RequireFiniteNonNegative(double value, string paramName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentException($"{paramName} must be a finite number.", paramName);
        if (value < 0)
            throw new ArgumentException($"{paramName} cannot be negative.", paramName);

        return value;
    }

    private static double? RequireFiniteNonNegativeOrNull(double? value, string paramName) =>
        value is { } v ? RequireFiniteNonNegative(v, paramName) : null;

    private static double? RequirePercentOrNull(double? value, string paramName)
    {
        if (value is null)
            return null;
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            throw new ArgumentException($"{paramName} must be a finite number.", paramName);
        if (value.Value is < 0 or > 100)
            throw new ArgumentException($"{paramName} must be between 0 and 100.", paramName);

        return value;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
