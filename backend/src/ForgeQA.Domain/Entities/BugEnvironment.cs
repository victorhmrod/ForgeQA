namespace ForgeQA.Domain.Entities;

/// <summary>
/// Diagnostic context captured automatically at report time. Every field is optional and
/// descriptive only — never used as an identifier. Deliberately excludes anything that could
/// identify a person or machine (no file paths, usernames, IP addresses, MAC/hardware serials).
/// </summary>
public class BugEnvironment
{
    public const int MapNameMaxLength = 200;
    public const int GameModeMaxLength = 100;
    public const int PlatformMaxLength = 50;
    public const int EngineVersionMaxLength = 50;
    public const int OsVersionMaxLength = 200;
    public const int CpuMaxLength = 200;
    public const int GpuMaxLength = 200;
    public const int LocaleMaxLength = 20;

    public string? MapName { get; private set; }
    public string? GameMode { get; private set; }
    public string? Platform { get; private set; }
    public string? EngineVersion { get; private set; }
    public string? OsVersion { get; private set; }
    public string? Cpu { get; private set; }
    public string? Gpu { get; private set; }
    public long? MemoryBytes { get; private set; }
    public string? Locale { get; private set; }

    private BugEnvironment() { }

    public BugEnvironment(
        string? mapName = null,
        string? gameMode = null,
        string? platform = null,
        string? engineVersion = null,
        string? osVersion = null,
        string? cpu = null,
        string? gpu = null,
        long? memoryBytes = null,
        string? locale = null)
    {
        MapName = Truncate(mapName, MapNameMaxLength);
        GameMode = Truncate(gameMode, GameModeMaxLength);
        Platform = Truncate(platform, PlatformMaxLength);
        EngineVersion = Truncate(engineVersion, EngineVersionMaxLength);
        OsVersion = Truncate(osVersion, OsVersionMaxLength);
        Cpu = Truncate(cpu, CpuMaxLength);
        Gpu = Truncate(gpu, GpuMaxLength);
        MemoryBytes = memoryBytes is > 0 ? memoryBytes : null;
        Locale = Truncate(locale, LocaleMaxLength);
    }

    public bool IsEmpty =>
        MapName is null && GameMode is null && Platform is null && EngineVersion is null &&
        OsVersion is null && Cpu is null && Gpu is null && MemoryBytes is null && Locale is null;

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
