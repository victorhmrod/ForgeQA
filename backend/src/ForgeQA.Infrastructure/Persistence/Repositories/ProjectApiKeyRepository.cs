using ForgeQA.Application.Abstractions;
using ForgeQA.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ForgeQA.Infrastructure.Persistence.Repositories;

public class ProjectApiKeyRepository : IProjectApiKeyRepository
{
    private readonly ForgeQADbContext _dbContext;

    public ProjectApiKeyRepository(ForgeQADbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ProjectApiKey?> GetByHashAsync(string keyHash, CancellationToken cancellationToken) =>
        _dbContext.ProjectApiKeys.FirstOrDefaultAsync(k => k.KeyHash == keyHash, cancellationToken);

    public Task<ProjectApiKey?> GetByIdForProjectAsync(Guid projectId, Guid keyId, CancellationToken cancellationToken) =>
        _dbContext.ProjectApiKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.ProjectId == projectId, cancellationToken);

    public Task<List<ProjectApiKey>> ListForProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        _dbContext.ProjectApiKeys.Where(k => k.ProjectId == projectId).OrderByDescending(k => k.CreatedAt).ToListAsync(cancellationToken);

    public void Add(ProjectApiKey apiKey) => _dbContext.ProjectApiKeys.Add(apiKey);
}
