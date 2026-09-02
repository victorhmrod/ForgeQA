using ForgeQA.Domain.Entities;

namespace ForgeQA.Application.Abstractions;

public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Organization?> GetBySlugAsync(string slug, CancellationToken cancellationToken);
    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);
    Task<List<Organization>> GetForUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<OrganizationMember?> GetMembershipAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    void Add(Organization organization);
}
