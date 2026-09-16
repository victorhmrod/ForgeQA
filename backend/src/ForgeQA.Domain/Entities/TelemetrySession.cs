using ForgeQA.Domain.Common;

namespace ForgeQA.Domain.Entities;

/// <summary>
/// One play session's worth of telemetry, correlated to the client-generated
/// <see cref="RuntimeSessionId"/> introduced in M4 — this is not a new client identifier, only the
/// server-side record of it. Belongs to exactly one Project and one Build; both are required and
/// immutable after construction (a session's identity never moves to a different Build). Events are
/// append-only; a session itself is never edited from the dashboard, only started and ended.
/// </summary>
public class TelemetrySession : Entity
{
    public Guid ProjectId { get; private set; }
    public Guid BuildId { get; private set; }
    public Guid RuntimeSessionId { get; private set; }

    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public DateTime? LastEventAt { get; private set; }

    public TelemetrySessionEnvironment Environment { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>An unclosed session (e.g. the game exited without calling End()) simply stays active
    /// forever — M5 has no automatic idle-timeout sweep. See docs/telemetry.md.</summary>
    public bool IsActive => EndedAt is null;

    private readonly List<TelemetryEvent> _events = new();
    public IReadOnlyCollection<TelemetryEvent> Events => _events.AsReadOnly();

    private TelemetrySession() { }

    public TelemetrySession(Guid projectId, Guid buildId, Guid runtimeSessionId, TelemetrySessionEnvironment? environment)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(projectId));
        if (buildId == Guid.Empty)
            throw new ArgumentException("BuildId is required.", nameof(buildId));
        if (runtimeSessionId == Guid.Empty)
            throw new ArgumentException("RuntimeSessionId is required.", nameof(runtimeSessionId));

        ProjectId = projectId;
        BuildId = buildId;
        RuntimeSessionId = runtimeSessionId;
        Environment = environment ?? new TelemetrySessionEnvironment();

        StartedAt = DateTime.UtcNow;
        CreatedAt = StartedAt;
        UpdatedAt = StartedAt;
    }

    /// <summary>Called once per accepted ingestion batch (never once per event) to keep write volume low.</summary>
    public void RecordEventsReceived()
    {
        LastEventAt = DateTime.UtcNow;
        UpdatedAt = LastEventAt.Value;
    }

    /// <summary>Idempotent — ending an already-ended session is a no-op, never an error.</summary>
    public void End()
    {
        if (EndedAt is not null)
            return;

        EndedAt = DateTime.UtcNow;
        UpdatedAt = EndedAt.Value;
    }
}
