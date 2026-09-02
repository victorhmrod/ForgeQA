using ForgeQA.Domain.Common;
using ForgeQA.Domain.Enums;

namespace ForgeQA.Domain.Entities;

public class OrganizationMember : Entity
{
    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public OrganizationRole Role { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private OrganizationMember() { }

    public OrganizationMember(Guid organizationId, Guid userId, OrganizationRole role)
    {
        OrganizationId = organizationId;
        UserId = userId;
        Role = role;
        CreatedAt = DateTime.UtcNow;
    }
}
