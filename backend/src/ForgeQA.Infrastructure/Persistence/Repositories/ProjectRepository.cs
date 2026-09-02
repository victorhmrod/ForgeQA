using ForgeQA.Application.Abstractions;
using ForgeQA.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ForgeQA.Infrastructure.Persistence.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly ForgeQADbContext _dbContext;

    public ProjectRepository(ForgeQADbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<List<Project>> GetForOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        _dbContext.Projects.Where(p => p.OrganizationId == organizationId).OrderBy(p => p.Name).ToListAsync(cancellationToken);

    public Task<bool> SlugExistsInOrganizationAsync(Guid organizationId, string slug, CancellationToken cancellationToken) =>
        _dbContext.Projects.AnyAsync(p => p.OrganizationId == organizationId && p.Slug == slug, cancellationToken);

    public void Add(Project project) => _dbContext.Projects.Add(project);
}
