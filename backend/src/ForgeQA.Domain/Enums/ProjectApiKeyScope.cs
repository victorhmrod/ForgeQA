namespace ForgeQA.Domain.Enums;

/// <summary>
/// Narrow, additive permissions a <see cref="Entities.ProjectApiKey"/> can hold. Deliberately small
/// for M4 — future milestones may add TELEMETRY_WRITE, CRASH_WRITE, BUILD_WRITE, etc., but a key
/// is never granted a scope it wasn't explicitly issued.
/// </summary>
public enum ProjectApiKeyScope
{
    BUG_REPORT_WRITE
}
