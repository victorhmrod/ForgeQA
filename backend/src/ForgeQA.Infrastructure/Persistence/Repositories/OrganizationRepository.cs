using ForgeQA.Application.Abstractions;
using ForgeQA.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ForgeQA.Infrastructure.Persistence.Repositories;

public class OrganizationRepository : IOrganizationRepository
{
    private readonly ForgeQADbContext _dbContext;

    public OrganizationRepository(ForgeQADbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.Organizations.Include(o => o.Members).FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task<Organization?> GetBySlugAsync(string slug, CancellationToken cancellationToken) =>
        _dbContext.Organizations.Include(o => o.Members).FirstOrDefaultAsync(o => o.Slug == slug, cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        _dbContext.Organizations.AnyAsync(o => o.Slug == slug, cancellationToken);

    public Task<List<Organization>> GetForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        _dbContext.Organizations
            .Include(o => o.Members)
            .Where(o => o.Members.Any(m => m.UserId == userId))
            .ToListAsync(cancellationToken);

    public Task<OrganizationMember?> GetMembershipAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        _dbContext.OrganizationMembers.FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId, cancellationToken);

    public void Add(Organization organization) => _dbContext.Organizations.Add(organization);
}
