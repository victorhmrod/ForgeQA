using ForgeQA.Application.Abstractions;
using ForgeQA.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ForgeQA.Infrastructure.Persistence.Repositories;

public class BugRepository : IBugRepository
{
    private readonly ForgeQADbContext _dbContext;

    public BugRepository(ForgeQADbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<BugReport?> GetByIdForProjectAsync(Guid projectId, Guid bugId, CancellationToken cancellationToken, bool includeAttachments = false)
    {
        var query = _dbContext.BugReports.Where(b => b.Id == bugId && b.ProjectId == projectId && b.DeletedAt == null);

        if (includeAttachments)
            query = query.Include(b => b.Attachments);

        return query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<BugReport> Items, int TotalCount)> QueryAsync(BugQuery query, CancellationToken cancellationToken)
    {
        var bugs = _dbContext.BugReports.AsNoTracking().Where(b => b.ProjectId == query.ProjectId && b.DeletedAt == null);

        if (query.Status is not null)
            bugs = bugs.Where(b => b.Status == query.Status);

        if (query.Severity is not null)
            bugs = bugs.Where(b => b.Severity == query.Severity);

        if (query.BuildId is not null)
            bugs = bugs.Where(b => b.BuildId == query.BuildId);

        if (query.Source is not null)
            bugs = bugs.Where(b => b.Source == query.Source);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            bugs = bugs.Where(b =>
                EF.Functions.ILike(b.Title, $"%{search}%") ||
                (b.Description != null && EF.Functions.ILike(b.Description, $"%{search}%")));
        }

        var totalCount = await bugs.CountAsync(cancellationToken);

        var items = await bugs
            .OrderByDescending(b => b.CreatedAt)
            .ThenByDescending(b => b.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public void Add(BugReport bugReport) => _dbContext.BugReports.Add(bugReport);

    public Task<BugAttachment?> GetAttachmentAsync(Guid bugReportId, Guid attachmentId, CancellationToken cancellationToken) =>
        _dbContext.BugAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.BugReportId == bugReportId, cancellationToken);

    public void Add(BugAttachment attachment) => _dbContext.BugAttachments.Add(attachment);
}
