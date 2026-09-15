using ForgeQA.Application.Abstractions;
using ForgeQA.Domain.Entities;
using ForgeQA.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ForgeQA.Infrastructure.Persistence.Repositories;

public class BuildRepository : IBuildRepository
{
    private readonly ForgeQADbContext _dbContext;

    public BuildRepository(ForgeQADbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Build?> GetByIdForProjectAsync(Guid projectId, Guid buildId, CancellationToken cancellationToken) =>
        _dbContext.Builds.FirstOrDefaultAsync(b => b.Id == buildId && b.ProjectId == projectId, cancellationToken);

    public async Task<(IReadOnlyList<Build> Items, int TotalCount)> QueryAsync(BuildQuery query, CancellationToken cancellationToken)
    {
        var builds = _dbContext.Builds.Where(b => b.ProjectId == query.ProjectId);

        builds = query.Status switch
        {
            BuildStatusFilter.Active => builds.Where(b => b.ArchivedAt == null),
            BuildStatusFilter.Archived => builds.Where(b => b.ArchivedAt != null),
            _ => builds
        };

        if (query.Platform is not null)
            builds = builds.Where(b => b.Platform == query.Platform);

        if (query.Configuration is not null)
            builds = builds.Where(b => b.Configuration == query.Configuration);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            builds = builds.Where(b =>
                EF.Functions.ILike(b.Version, $"%{search}%") ||
                EF.Functions.ILike(b.BuildNumber, $"%{search}%") ||
                (b.Name != null && EF.Functions.ILike(b.Name, $"%{search}%")) ||
                (b.Branch != null && EF.Functions.ILike(b.Branch, $"%{search}%")) ||
                (b.CommitSha != null && EF.Functions.ILike(b.CommitSha, $"%{search}%")));
        }

        var totalCount = await builds.CountAsync(cancellationToken);

        var items = await builds
            .OrderByDescending(b => b.CreatedAt)
            .ThenByDescending(b => b.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<bool> ExistsAsync(Guid projectId, string buildNumber, BuildPlatform platform, BuildConfiguration configuration, CancellationToken cancellationToken) =>
        _dbContext.Builds.AnyAsync(
            b => b.ProjectId == projectId && b.BuildNumber == buildNumber && b.Platform == platform && b.Configuration == configuration,
            cancellationToken);

    public void Add(Build build) => _dbContext.Builds.Add(build);
}
