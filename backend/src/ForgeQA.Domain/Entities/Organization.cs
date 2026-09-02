using ForgeQA.Domain.Common;
using ForgeQA.Domain.Enums;

namespace ForgeQA.Domain.Entities;

public class Organization : Entity
{
    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<OrganizationMember> _members = new();
    public IReadOnlyCollection<OrganizationMember> Members => _members.AsReadOnly();

    private readonly List<Project> _projects = new();
    public IReadOnlyCollection<Project> Projects => _projects.AsReadOnly();

    private Organization() { }

    public Organization(string name, string slug)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Organization name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Organization slug is required.", nameof(slug));

        Name = name;
        Slug = slug;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public OrganizationMember AddMember(Guid userId, OrganizationRole role)
    {
        if (_members.Any(m => m.UserId == userId))
            throw new InvalidOperationException("User is already a member of this organization.");

        var member = new OrganizationMember(Id, userId, role);
        _members.Add(member);
        UpdatedAt = DateTime.UtcNow;
        return member;
    }
}
