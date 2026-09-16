using System.Text.RegularExpressions;
using ForgeQA.Domain.Common;

namespace ForgeQA.Domain.Entities;

/// <summary>
/// One structured, immutable telemetry event within a <see cref="TelemetrySession"/>. Never edited
/// or deleted from the dashboard — telemetry is append-only. <see cref="SequenceNumber"/> is
/// assigned by the client and is unique within its session (enforced at the persistence layer),
/// letting the backend/UI reconstruct true event order even across batched delivery, retries, and
/// clock drift; <see cref="ClientTimestamp"/> is never trusted as server truth on its own, which is
/// why <see cref="ReceivedAt"/> is always recorded independently.
/// </summary>
public class TelemetryEvent : Entity
{
    public const int EventNameMaxLength = 128;
    public const int CategoryMaxLength = 64;
    public const int MapNameMaxLength = 200;

    private static readonly Regex EventNamePattern = new("^[A-Za-z0-9._-]{1,128}$", RegexOptions.Compiled);

    public Guid TelemetrySessionId { get; private set; }
    public int SequenceNumber { get; private set; }
    public string EventName { get; private set; } = null!;
    public DateTime ClientTimestamp { get; private set; }
    public DateTime ReceivedAt { get; private set; }
    public string? Category { get; private set; }

    /// <summary>Raw, already-validated JSON object text (never a bare scalar). Application layer
    /// owns parsing/re-serialization; Domain only enforces "non-empty, well-formed" via the
    /// constructor's caller contract, keeping this class free of a JSON library dependency.</summary>
    public string PropertiesJson { get; private set; } = "{}";

    public string? MapName { get; private set; }

    private TelemetryEvent() { }

    public TelemetryEvent(
        Guid telemetrySessionId,
        int sequenceNumber,
        string eventName,
        DateTime clientTimestamp,
        string? category,
        string propertiesJson,
        string? mapName)
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

        SetEventName(eventName);
        SetCategory(category);
        SetMapName(mapName);
        PropertiesJson = string.IsNullOrWhiteSpace(propertiesJson) ? "{}" : propertiesJson;
    }

    private void SetEventName(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            throw new ArgumentException("EventName is required.", nameof(eventName));
        if (!EventNamePattern.IsMatch(eventName))
            throw new ArgumentException(
                $"EventName must be 1-{EventNameMaxLength} characters using only letters, numbers, '.', '_' or '-'.", nameof(eventName));

        EventName = eventName;
    }

    private void SetCategory(string? category)
    {
        if (category is not null && category.Length > CategoryMaxLength)
            throw new ArgumentException($"Category must be at most {CategoryMaxLength} characters.", nameof(category));

        Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
    }

    private void SetMapName(string? mapName)
    {
        if (mapName is not null && mapName.Length > MapNameMaxLength)
            throw new ArgumentException($"MapName must be at most {MapNameMaxLength} characters.", nameof(mapName));

        MapName = string.IsNullOrWhiteSpace(mapName) ? null : mapName.Trim();
    }
}
