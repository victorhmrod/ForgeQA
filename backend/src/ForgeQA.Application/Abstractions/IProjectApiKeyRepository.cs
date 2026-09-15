using ForgeQA.Domain.Entities;

namespace ForgeQA.Application.Abstractions;

public interface IProjectApiKeyRepository
{
    Task<ProjectApiKey?> GetByHashAsync(string keyHash, CancellationToken cancellationToken);
    Task<ProjectApiKey?> GetByIdForProjectAsync(Guid projectId, Guid keyId, CancellationToken cancellationToken);
    Task<List<ProjectApiKey>> ListForProjectAsync(Guid projectId, CancellationToken cancellationToken);
    void Add(ProjectApiKey apiKey);
}
