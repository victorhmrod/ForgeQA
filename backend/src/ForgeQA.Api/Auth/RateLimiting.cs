namespace ForgeQA.Api.Auth;

public static class RateLimiting
{
    /// <summary>Applied to internet-facing runtime bug-ingestion endpoints. See docs/bug-reporting.md.</summary>
    public const string BugIngestionPolicy = "BugIngestion";
}
