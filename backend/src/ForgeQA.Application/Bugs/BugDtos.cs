using ForgeQA.Domain.Enums;

namespace ForgeQA.Application.Bugs;

public record BugEnvironmentDto(
    string? MapName,
    string? GameMode,
    string? Platform,
    string? EngineVersion,
    string? OsVersion,
    string? Cpu,
    string? Gpu,
    long? MemoryBytes,
    string? Locale);

public record CreateBugRequest(
    Guid? BuildId,
    string Title,
    string? Description,
    string? ReproductionSteps,
    BugSeverity Severity,
    BugSource Source,
    string? ReporterDisplayName,
    Guid? RuntimeSessionId,
    BugEnvironmentDto? Environment);

public record UpdateBugRequest(
    string Title,
    string? Description,
    string? ReproductionSteps,
    BugSeverity Severity,
    BugStatus Status);

public record BugBuildSummaryResponse(Guid Id, string Version, string BuildNumber, BuildPlatform Platform, BuildConfiguration Configuration);

public record BugReporterResponse(Guid? Id, string? DisplayName);

public record BugAttachmentResponse(
    Guid Id,
    BugAttachmentType Type,
    string FileName,
    string ContentType,
    long SizeBytes,
    BugAttachmentStatus Status,
    DateTime CreatedAt);

public record BugListItemResponse(
    Guid Id,
    Guid ProjectId,
    string Title,
    BugSeverity Severity,
    BugStatus Status,
    BugSource Source,
    BugBuildSummaryResponse? Build,
    BugReporterResponse Reporter,
    DateTime CreatedAt);

public record BugResponse(
    Guid Id,
    Guid ProjectId,
    BugBuildSummaryResponse? Build,
    string Title,
    string? Description,
    string? ReproductionSteps,
    BugSeverity Severity,
    BugStatus Status,
    BugSource Source,
    Guid? RuntimeSessionId,
    BugEnvironmentDto Environment,
    BugReporterResponse Reporter,
    IReadOnlyList<BugAttachmentResponse> Attachments,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ResolvedAt,
    DateTime? ClosedAt);

public record ListBugsRequest(
    int Page,
    int PageSize,
    BugStatus? Status,
    BugSeverity? Severity,
    Guid? BuildId,
    BugSource? Source,
    string? Search);

public record InitiateBugAttachmentRequest(BugAttachmentType Type, string FileName, string ContentType, long SizeBytes);
public record InitiateBugAttachmentResponse(Guid AttachmentId, string UploadUrl, DateTime ExpiresAt);
public record BugAttachmentDownloadResponse(string Url, DateTime ExpiresAt);
