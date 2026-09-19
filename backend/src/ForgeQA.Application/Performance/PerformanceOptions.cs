namespace ForgeQA.Application.Performance;

public sealed class PerformanceOptions
{
    public const string SectionName = "Performance";

    public int MaxSamplesPerBatch { get; set; } = 120;
    public long MaxBatchBytes { get; set; } = 1 * 1024 * 1024;

    /// <summary>Performance ingestion batches allowed per Project API key per minute.</summary>
    public int MaxBatchesPerMinutePerKey { get; set; } = 120;
}
