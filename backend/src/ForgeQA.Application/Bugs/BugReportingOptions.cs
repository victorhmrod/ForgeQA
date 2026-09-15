namespace ForgeQA.Application.Bugs;

public sealed class BugReportingOptions
{
    public const string SectionName = "BugReporting";

    public long MaxScreenshotSizeBytes { get; set; } = 20 * 1024 * 1024;
    public int AttachmentUploadUrlLifetimeMinutes { get; set; } = 15;
    public int AttachmentDownloadUrlLifetimeMinutes { get; set; } = 10;

    /// <summary>Runtime bug submissions allowed per Project API key per minute. See docs/bug-reporting.md.</summary>
    public int MaxRuntimeReportsPerMinutePerKey { get; set; } = 60;
}
