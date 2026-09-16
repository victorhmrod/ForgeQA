namespace ForgeQA.Domain.Entities;

/// <summary>
/// An immutable snapshot of runtime context captured once when a <see cref="TelemetrySession"/>
/// starts. Deliberately does not duplicate a Build's own Version/BuildNumber/Branch/CommitSha —
/// those are joined from the correlated Build when needed. Every field is optional and descriptive
/// only, never used as an identifier; never captures anything that could identify a person or
/// machine (no file paths, usernames, IP addresses, hardware serials).
/// </summary>
public class TelemetrySessionEnvironment
{
    public const int MapNameMaxLength = 200;
    public const int GameModeMaxLength = 100;
    public const int PlatformMaxLength = 50;
    public const int ConfigurationMaxLength = 50;
    public const int EngineVersionMaxLength = 50;
    public const int OsVersionMaxLength = 200;
    public const int LocaleMaxLength = 20;

    public string? MapName { get; private set; }
    public string? GameMode { get; private set; }
    public string? Platform { get; private set; }
    public string? Configuration { get; private set; }
    public string? EngineVersion { get; private set; }
    public string? OsVersion { get; private set; }
    public string? Locale { get; private set; }

    private TelemetrySessionEnvironment() { }

    public TelemetrySessionEnvironment(
        string? mapName = null,
        string? gameMode = null,
        string? platform = null,
        string? configuration = null,
        string? engineVersion = null,
        string? osVersion = null,
        string? locale = null)
    {
        MapName = Truncate(mapName, MapNameMaxLength);
        GameMode = Truncate(gameMode, GameModeMaxLength);
        Platform = Truncate(platform, PlatformMaxLength);
        Configuration = Truncate(configuration, ConfigurationMaxLength);
        EngineVersion = Truncate(engineVersion, EngineVersionMaxLength);
        OsVersion = Truncate(osVersion, OsVersionMaxLength);
        Locale = Truncate(locale, LocaleMaxLength);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
