using ForgeQA.Application.Abstractions;
using ForgeQA.Domain.Entities;

namespace ForgeQA.UnitTests.Application;

public class FakeOrganizationRepository : IOrganizationRepository
{
    public List<Organization> Organizations { get; } = new();

    public Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Organizations.FirstOrDefault(o => o.Id == id));

    public Task<Organization?> GetBySlugAsync(string slug, CancellationToken cancellationToken) =>
        Task.FromResult(Organizations.FirstOrDefault(o => o.Slug == slug));

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        Task.FromResult(Organizations.Any(o => o.Slug == slug));

    public Task<List<Organization>> GetForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Organizations.Where(o => o.Members.Any(m => m.UserId == userId)).ToList());

    public Task<OrganizationMember?> GetMembershipAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Organizations.FirstOrDefault(o => o.Id == organizationId)?.Members.FirstOrDefault(m => m.UserId == userId));

    public void Add(Organization organization) => Organizations.Add(organization);
}
