using ForgeQA.Domain.Enums;
using ForgeQA.Domain.Common;

namespace ForgeQA.Domain.Entities;

public class Build : Entity
{
    public const int NameMaxLength = 200;
    public const int VersionMaxLength = 100;
    public const int BuildNumberMaxLength = 100;
    public const int BranchMaxLength = 200;
    public const int CommitShaMaxLength = 100;
    public const int EngineVersionMaxLength = 50;
    public const int ChangelogMaxLength = 4000;

    public Guid ProjectId { get; private set; }

    public string? Name { get; private set; }
    public string Version { get; private set; } = null!;
    public string BuildNumber { get; private set; } = null!;

    public BuildPlatform Platform { get; private set; }
    public BuildConfiguration Configuration { get; private set; }

    public string? Branch { get; private set; }
    public string? CommitSha { get; private set; }

    public string? EngineVersion { get; private set; }
    public string? Changelog { get; private set; }

    public Guid CreatedByUserId { get; private set; }
    public string CreatedByDisplayName { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? ArchivedAt { get; private set; }

    public bool IsArchived => ArchivedAt is not null;

    private Build() { }

    public Build(
        Guid projectId,
        string version,
        string buildNumber,
        BuildPlatform platform,
        BuildConfiguration configuration,
        Guid createdByUserId,
        string createdByDisplayName,
        string? name = null,
        string? branch = null,
        string? commitSha = null,
        string? engineVersion = null,
        string? changelog = null)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(projectId));
        if (createdByUserId == Guid.Empty)
            throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));

        ProjectId = projectId;
        CreatedByUserId = createdByUserId;
        CreatedByDisplayName = createdByDisplayName;
        Platform = platform;
        Configuration = configuration;

        SetVersion(version);
        SetBuildNumber(buildNumber);
        SetName(name);
        SetSourceMetadata(branch, commitSha);
        SetEngineVersion(engineVersion);
        SetChangelog(changelog);

        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void UpdateMetadata(
        string version,
        string? name,
        string? branch,
        string? commitSha,
        string? engineVersion,
        string? changelog)
    {
        SetVersion(version);
        SetName(name);
        SetSourceMetadata(branch, commitSha);
        SetEngineVersion(engineVersion);
        SetChangelog(changelog);

        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        if (IsArchived)
            return;

        ArchivedAt = DateTime.UtcNow;
        UpdatedAt = ArchivedAt.Value;
    }

    public void Restore()
    {
        if (!IsArchived)
            return;

        ArchivedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    private void SetVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Version is required.", nameof(version));
        if (version.Length > VersionMaxLength)
            throw new ArgumentException($"Version must be at most {VersionMaxLength} characters.", nameof(version));

        Version = version.Trim();
    }

    private void SetBuildNumber(string buildNumber)
    {
        if (string.IsNullOrWhiteSpace(buildNumber))
            throw new ArgumentException("Build number is required.", nameof(buildNumber));
        if (buildNumber.Length > BuildNumberMaxLength)
            throw new ArgumentException($"Build number must be at most {BuildNumberMaxLength} characters.", nameof(buildNumber));

        BuildNumber = buildNumber.Trim();
    }

    private void SetName(string? name)
    {
        if (name is not null && name.Length > NameMaxLength)
            throw new ArgumentException($"Name must be at most {NameMaxLength} characters.", nameof(name));

        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }

    private void SetSourceMetadata(string? branch, string? commitSha)
    {
        if (branch is not null && branch.Length > BranchMaxLength)
            throw new ArgumentException($"Branch must be at most {BranchMaxLength} characters.", nameof(branch));
        if (commitSha is not null && commitSha.Length > CommitShaMaxLength)
            throw new ArgumentException($"CommitSha must be at most {CommitShaMaxLength} characters.", nameof(commitSha));

        Branch = string.IsNullOrWhiteSpace(branch) ? null : branch.Trim();
        CommitSha = string.IsNullOrWhiteSpace(commitSha) ? null : commitSha.Trim();
    }

    private void SetEngineVersion(string? engineVersion)
    {
        if (engineVersion is not null && engineVersion.Length > EngineVersionMaxLength)
            throw new ArgumentException($"EngineVersion must be at most {EngineVersionMaxLength} characters.", nameof(engineVersion));

        EngineVersion = string.IsNullOrWhiteSpace(engineVersion) ? null : engineVersion.Trim();
    }

    private void SetChangelog(string? changelog)
    {
        if (changelog is not null && changelog.Length > ChangelogMaxLength)
            throw new ArgumentException($"Changelog must be at most {ChangelogMaxLength} characters.", nameof(changelog));

        Changelog = string.IsNullOrWhiteSpace(changelog) ? null : changelog.Trim();
    }
}
