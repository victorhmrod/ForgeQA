using ForgeQA.Domain.Common;

namespace ForgeQA.Domain.Entities;

public class Session : Entity
{
    public Guid ProjectId { get; private set; }
    public Guid? BuildId { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Session() { }

    public Session(Guid projectId, Guid? buildId, DateTime startedAt)
    {
        ProjectId = projectId;
        BuildId = buildId;
        StartedAt = startedAt;
        CreatedAt = DateTime.UtcNow;
    }
}
