using ForgeQA.Domain.Common;

namespace ForgeQA.Domain.Entities;

public class Build : Entity
{
    public Guid ProjectId { get; private set; }
    public string Version { get; private set; } = null!;
    public string? Branch { get; private set; }
    public string? CommitHash { get; private set; }
    public string Platform { get; private set; } = null!;
    public string Configuration { get; private set; } = null!;
    public string? EngineVersion { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Build() { }

    public Build(Guid projectId, string version, string platform, string configuration, string? branch = null, string? commitHash = null, string? engineVersion = null)
    {
        ProjectId = projectId;
        Version = version;
        Platform = platform;
        Configuration = configuration;
        Branch = branch;
        CommitHash = commitHash;
        EngineVersion = engineVersion;
        CreatedAt = DateTime.UtcNow;
    }
}
