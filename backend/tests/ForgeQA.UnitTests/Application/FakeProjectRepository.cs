using ForgeQA.Application.Abstractions;
using ForgeQA.Domain.Entities;

namespace ForgeQA.UnitTests.Application;

public class FakeProjectRepository : IProjectRepository
{
    public List<Project> Projects { get; } = new();

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Projects.FirstOrDefault(p => p.Id == id));

    public Task<List<Project>> GetForOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult(Projects.Where(p => p.OrganizationId == organizationId).ToList());

    public Task<bool> SlugExistsInOrganizationAsync(Guid organizationId, string slug, CancellationToken cancellationToken) =>
        Task.FromResult(Projects.Any(p => p.OrganizationId == organizationId && p.Slug == slug));

    public void Add(Project project) => Projects.Add(project);
}
