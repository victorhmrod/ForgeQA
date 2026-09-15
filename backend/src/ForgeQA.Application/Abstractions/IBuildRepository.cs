using ForgeQA.Domain.Entities;
using ForgeQA.Domain.Enums;

namespace ForgeQA.Application.Abstractions;

public enum BuildStatusFilter
{
    Active,
    Archived,
    All
}

public record BuildQuery(
    Guid ProjectId,
    int Page,
    int PageSize,
    BuildPlatform? Platform,
    BuildConfiguration? Configuration,
    BuildStatusFilter Status,
    string? Search);

public interface IBuildRepository
{
    Task<Build?> GetByIdForProjectAsync(Guid projectId, Guid buildId, CancellationToken cancellationToken);
    Task<List<Build>> GetByIdsAsync(IReadOnlyCollection<Guid> buildIds, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Build> Items, int TotalCount)> QueryAsync(BuildQuery query, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid projectId, string buildNumber, BuildPlatform platform, BuildConfiguration configuration, CancellationToken cancellationToken);
    void Add(Build build);
}
