using ForgeQA.Application.Abstractions;
using ForgeQA.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ForgeQA.Infrastructure.Persistence.Repositories;

public class ArtifactRepository : IArtifactRepository
{
    private readonly ForgeQADbContext _dbContext;

    public ArtifactRepository(ForgeQADbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<BuildArtifact>> ListForBuildAsync(Guid buildId, CancellationToken cancellationToken) =>
        await _dbContext.BuildArtifacts
            .Where(a => a.BuildId == buildId && a.DeletedAt == null)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .ToListAsync(cancellationToken);

    public Task<BuildArtifact?> GetByIdForBuildAsync(Guid buildId, Guid artifactId, CancellationToken cancellationToken, bool includeDeleted = false)
    {
        var query = _dbContext.BuildArtifacts.Where(a => a.BuildId == buildId && a.Id == artifactId);
        if (!includeDeleted)
            query = query.Where(a => a.DeletedAt == null);

        return query.FirstOrDefaultAsync(cancellationToken);
    }

    public Task<ArtifactUploadSession?> GetUploadSessionAsync(Guid artifactId, Guid sessionId, CancellationToken cancellationToken) =>
        _dbContext.ArtifactUploadSessions.FirstOrDefaultAsync(s => s.ArtifactId == artifactId && s.Id == sessionId, cancellationToken);

    public Task<ArtifactUploadSession?> GetActiveUploadSessionAsync(Guid artifactId, CancellationToken cancellationToken) =>
        _dbContext.ArtifactUploadSessions
            .Where(s => s.ArtifactId == artifactId && s.CompletedAt == null && s.AbortedAt == null)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(BuildArtifact artifact) => _dbContext.BuildArtifacts.Add(artifact);
    public void Add(ArtifactUploadSession session) => _dbContext.ArtifactUploadSessions.Add(session);
}
