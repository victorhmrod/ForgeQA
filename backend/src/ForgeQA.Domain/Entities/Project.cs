using ForgeQA.Domain.Common;

namespace ForgeQA.Domain.Entities;

public class Project : Entity
{
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Project() { }

    public Project(Guid organizationId, string name, string slug, string? description)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Project slug is required.", nameof(slug));

        OrganizationId = organizationId;
        Name = name;
        Slug = slug;
        Description = description;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }
}
