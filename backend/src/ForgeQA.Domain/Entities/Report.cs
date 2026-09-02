using ForgeQA.Domain.Common;
using ForgeQA.Domain.Enums;

namespace ForgeQA.Domain.Entities;

public class Report : Entity
{
    public Guid ProjectId { get; private set; }
    public Guid? BuildId { get; private set; }
    public Guid? SessionId { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public ReportStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Report() { }

    public Report(Guid projectId, string title, string? description, Guid? buildId = null, Guid? sessionId = null)
    {
        ProjectId = projectId;
        Title = title;
        Description = description;
        BuildId = buildId;
        SessionId = sessionId;
        Status = ReportStatus.Open;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }
}
