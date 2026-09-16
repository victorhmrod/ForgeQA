namespace ForgeQA.Application.Telemetry;

public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    public int MaxEventsPerBatch { get; set; } = 100;
    public long MaxBatchBytes { get; set; } = 1 * 1024 * 1024;
    public int MaxEventPropertiesBytes { get; set; } = 32 * 1024;

    /// <summary>Telemetry batches allowed per Project API key per minute. See docs/telemetry.md.</summary>
    public int MaxBatchesPerMinutePerKey { get; set; } = 120;
}
