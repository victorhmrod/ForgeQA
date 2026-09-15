using ForgeQA.Domain.Entities;

namespace ForgeQA.Application.Abstractions;

public interface IArtifactRepository
{
    Task<IReadOnlyList<BuildArtifact>> ListForBuildAsync(Guid buildId, CancellationToken cancellationToken);
    Task<BuildArtifact?> GetByIdForBuildAsync(Guid buildId, Guid artifactId, CancellationToken cancellationToken, bool includeDeleted = false);
    Task<ArtifactUploadSession?> GetUploadSessionAsync(Guid artifactId, Guid sessionId, CancellationToken cancellationToken);
    Task<ArtifactUploadSession?> GetActiveUploadSessionAsync(Guid artifactId, CancellationToken cancellationToken);
    void Add(BuildArtifact artifact);
    void Add(ArtifactUploadSession session);
}
