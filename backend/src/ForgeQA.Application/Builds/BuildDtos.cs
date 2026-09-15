using ForgeQA.Domain.Enums;

namespace ForgeQA.Application.Builds;

public record CreateBuildRequest(
    string? Name,
    string Version,
    string BuildNumber,
    BuildPlatform Platform,
    BuildConfiguration Configuration,
    string? Branch,
    string? CommitSha,
    string? EngineVersion,
    string? Changelog);

public record UpdateBuildRequest(
    string Version,
    string? Name,
    string? Branch,
    string? CommitSha,
    string? EngineVersion,
    string? Changelog);

public record BuildSourceResponse(string? Branch, string? CommitSha);

public record BuildCreatedByResponse(Guid Id, string Name);

public record BuildResponse(
    Guid Id,
    Guid ProjectId,
    string? Name,
    string Version,
    string BuildNumber,
    BuildPlatform Platform,
    BuildConfiguration Configuration,
    BuildSourceResponse Source,
    string? EngineVersion,
    string? Changelog,
    DateTime? ArchivedAt,
    BuildCreatedByResponse CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record ListBuildsRequest(
    int Page,
    int PageSize,
    BuildPlatform? Platform,
    BuildConfiguration? Configuration,
    string? Status,
    string? Search);
