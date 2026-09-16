namespace ForgeQA.Api.Auth;

public static class RateLimiting
{
    /// <summary>Applied to internet-facing runtime bug-ingestion endpoints. See docs/bug-reporting.md.</summary>
    public const string BugIngestionPolicy = "BugIngestion";

    /// <summary>Applied to internet-facing runtime telemetry-ingestion endpoints. Telemetry has a
    /// much higher expected volume than bug reports, so it gets its own, more permissive policy —
    /// see docs/telemetry.md.</summary>
    public const string TelemetryIngestionPolicy = "TelemetryIngestion";
}
