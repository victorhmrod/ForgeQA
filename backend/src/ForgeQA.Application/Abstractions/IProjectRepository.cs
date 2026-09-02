using ForgeQA.Domain.Entities;

namespace ForgeQA.Application.Abstractions;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<List<Project>> GetForOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<bool> SlugExistsInOrganizationAsync(Guid organizationId, string slug, CancellationToken cancellationToken);
    void Add(Project project);
}
