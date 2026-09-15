using ForgeQA.Domain.Common;
using ForgeQA.Domain.Enums;

namespace ForgeQA.Domain.Entities;

public class BugReport : Entity
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 8000;
    public const int ReproductionStepsMaxLength = 8000;
    public const int ReporterDisplayNameMaxLength = 200;

    public Guid ProjectId { get; private set; }
    public Guid? BuildId { get; private set; }

    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public string? ReproductionSteps { get; private set; }

    public BugSeverity Severity { get; private set; }
    public BugStatus Status { get; private set; }
    public BugSource Source { get; private set; }

    public Guid? ReporterUserId { get; private set; }
    public string? ReporterDisplayName { get; private set; }

    public Guid? RuntimeSessionId { get; private set; }

    public BugEnvironment Environment { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    private readonly List<BugAttachment> _attachments = new();
    public IReadOnlyCollection<BugAttachment> Attachments => _attachments.AsReadOnly();

    private BugReport() { }

    public BugReport(
        Guid projectId,
        string title,
        BugSeverity severity,
        BugSource source,
        Guid? buildId,
        string? description,
        string? reproductionSteps,
        Guid? reporterUserId,
        string? reporterDisplayName,
        Guid? runtimeSessionId,
        BugEnvironment? environment)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(projectId));
        if (source == BugSource.UNREAL_RUNTIME && buildId is null)
            throw new ArgumentException("A build-linked runtime report requires a valid BuildId.", nameof(buildId));

        ProjectId = projectId;
        BuildId = buildId;
        Severity = severity;
        Source = source;
        Status = BugStatus.OPEN;
        ReporterUserId = reporterUserId;
        RuntimeSessionId = runtimeSessionId;
        Environment = environment ?? new BugEnvironment();

        SetTitle(title);
        SetDescription(description);
        SetReproductionSteps(reproductionSteps);
        SetReporterDisplayName(reporterDisplayName);

        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    /// <summary>
    /// Updates only human-authored fields. Identity, source, Build correlation, reporter, the
    /// captured runtime environment, and timestamps are never mutated here — they are the
    /// immutable historical record of what happened, and remain absent from this method's
    /// signature so mass-assignment of those fields is structurally impossible.
    /// </summary>
    public void UpdateDetails(string title, string? description, string? reproductionSteps, BugSeverity severity)
    {
        SetTitle(title);
        SetDescription(description);
        SetReproductionSteps(reproductionSteps);
        Severity = severity;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangeStatus(BugStatus newStatus)
    {
        if (newStatus == Status)
            return;

        Status = newStatus;

        switch (newStatus)
        {
            case BugStatus.RESOLVED:
                ResolvedAt = DateTime.UtcNow;
                break;
            case BugStatus.CLOSED:
                ClosedAt = DateTime.UtcNow;
                ResolvedAt ??= ClosedAt;
                break;
            case BugStatus.OPEN:
            case BugStatus.IN_PROGRESS:
                // Reopening clears the terminal timestamps: a bug moved back to OPEN or
                // IN_PROGRESS is, by definition, no longer resolved or closed.
                ResolvedAt = null;
                ClosedAt = null;
                break;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void AddAttachment(BugAttachment attachment)
    {
        _attachments.Add(attachment);
    }

    public void SoftDelete()
    {
        if (IsDeleted)
            return;

        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DeletedAt.Value;
    }

    private void SetTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (title.Length > TitleMaxLength)
            throw new ArgumentException($"Title must be at most {TitleMaxLength} characters.", nameof(title));

        Title = title.Trim();
    }

    private void SetDescription(string? description)
    {
        if (description is not null && description.Length > DescriptionMaxLength)
            throw new ArgumentException($"Description must be at most {DescriptionMaxLength} characters.", nameof(description));

        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    private void SetReproductionSteps(string? reproductionSteps)
    {
        if (reproductionSteps is not null && reproductionSteps.Length > ReproductionStepsMaxLength)
            throw new ArgumentException($"Reproduction steps must be at most {ReproductionStepsMaxLength} characters.", nameof(reproductionSteps));

        ReproductionSteps = string.IsNullOrWhiteSpace(reproductionSteps) ? null : reproductionSteps.Trim();
    }

    private void SetReporterDisplayName(string? reporterDisplayName)
    {
        if (reporterDisplayName is not null && reporterDisplayName.Length > ReporterDisplayNameMaxLength)
            throw new ArgumentException($"Reporter display name must be at most {ReporterDisplayNameMaxLength} characters.", nameof(reporterDisplayName));

        ReporterDisplayName = string.IsNullOrWhiteSpace(reporterDisplayName) ? null : reporterDisplayName.Trim();
    }
}
