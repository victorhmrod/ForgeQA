using ForgeQA.Domain.Entities;
using ForgeQA.Domain.Enums;

namespace ForgeQA.Application.Abstractions;

public record BugQuery(
    Guid ProjectId,
    int Page,
    int PageSize,
    BugStatus? Status,
    BugSeverity? Severity,
    Guid? BuildId,
    BugSource? Source,
    string? Search);

public interface IBugRepository
{
    Task<BugReport?> GetByIdForProjectAsync(Guid projectId, Guid bugId, CancellationToken cancellationToken, bool includeAttachments = false);
    Task<(IReadOnlyList<BugReport> Items, int TotalCount)> QueryAsync(BugQuery query, CancellationToken cancellationToken);
    void Add(BugReport bugReport);

    Task<BugAttachment?> GetAttachmentAsync(Guid bugReportId, Guid attachmentId, CancellationToken cancellationToken);
    void Add(BugAttachment attachment);
}
