namespace ForgeQA.Domain.Enums;

/// <summary>
/// Narrow, additive permissions a <see cref="Entities.ProjectApiKey"/> can hold. A key is never
/// granted a scope it wasn't explicitly issued — adding a new scope here does not retroactively
/// grant it to any existing key. Future milestones may add CRASH_WRITE, BUILD_WRITE, etc.
/// </summary>
public enum ProjectApiKeyScope
{
    BUG_REPORT_WRITE,
    TELEMETRY_WRITE,
    PERFORMANCE_WRITE
}
