using ForgeQA.Application.Abstractions;
using ForgeQA.Application.Common;
using ForgeQA.Domain.Entities;

namespace ForgeQA.Application.Builds;

public class BuildService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly IBuildRepository _buildRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IIdentityService _identityService;
    private readonly IUnitOfWork _unitOfWork;

    public BuildService(
        IBuildRepository buildRepository,
        IProjectRepository projectRepository,
        IOrganizationRepository organizationRepository,
        IIdentityService identityService,
        IUnitOfWork unitOfWork)
    {
        _buildRepository = buildRepository;
        _projectRepository = projectRepository;
        _organizationRepository = organizationRepository;
        _identityService = identityService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BuildResponse>> CreateAsync(Guid projectId, Guid userId, CreateBuildRequest request, CancellationToken cancellationToken)
    {
        var access = await AuthorizeProjectAsync(projectId, userId, cancellationToken);
        if (!access.IsSuccess)
            return Result<BuildResponse>.Failure(access.ErrorType, access.Error!);

        var duplicate = await _buildRepository.ExistsAsync(projectId, request.BuildNumber, request.Platform, request.Configuration, cancellationToken);
        if (duplicate)
        {
            return Result<BuildResponse>.Failure(
                ErrorType.Conflict,
                "A build with the same build number, platform, and configuration is already registered for this project.");
        }

        var creator = await _identityService.GetUserByIdAsync(userId, cancellationToken);
        var creatorName = creator?.DisplayName ?? "Unknown";

        Build build;
        try
        {
            build = new Build(
                projectId,
                request.Version,
                request.BuildNumber,
                request.Platform,
                request.Configuration,
                userId,
                creatorName,
                request.Name,
                request.Branch,
                request.CommitSha,
                request.EngineVersion,
                request.Changelog);
        }
        catch (ArgumentException ex)
        {
            return Result<BuildResponse>.Failure(ErrorType.Validation, ex.Message);
        }

        _buildRepository.Add(build);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<BuildResponse>.Success(ToResponse(build));
    }

    public async Task<Result<PagedResult<BuildResponse>>> GetForProjectAsync(Guid projectId, Guid userId, ListBuildsRequest request, CancellationToken cancellationToken)
    {
        var access = await AuthorizeProjectAsync(projectId, userId, cancellationToken);
        if (!access.IsSuccess)
            return Result<PagedResult<BuildResponse>>.Failure(access.ErrorType, access.Error!);

        var status = ParseStatus(request.Status);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? DefaultPageSize : Math.Min(request.PageSize, MaxPageSize);

        var query = new BuildQuery(projectId, page, pageSize, request.Platform, request.Configuration, status, request.Search);
        var (items, totalCount) = await _buildRepository.QueryAsync(query, cancellationToken);

        var result = new PagedResult<BuildResponse>(items.Select(ToResponse).ToList(), page, pageSize, totalCount);
        return Result<PagedResult<BuildResponse>>.Success(result);
    }

    public async Task<Result<BuildResponse>> GetByIdAsync(Guid projectId, Guid buildId, Guid userId, CancellationToken cancellationToken)
    {
        var access = await AuthorizeProjectAsync(projectId, userId, cancellationToken);
        if (!access.IsSuccess)
            return Result<BuildResponse>.Failure(access.ErrorType, access.Error!);

        var build = await _buildRepository.GetByIdForProjectAsync(projectId, buildId, cancellationToken);
        if (build is null)
            return Result<BuildResponse>.Failure(ErrorType.NotFound, "Build not found.");

        return Result<BuildResponse>.Success(ToResponse(build));
    }

    public async Task<Result<BuildResponse>> UpdateAsync(Guid projectId, Guid buildId, Guid userId, UpdateBuildRequest request, CancellationToken cancellationToken)
    {
        var access = await AuthorizeProjectAsync(projectId, userId, cancellationToken);
        if (!access.IsSuccess)
            return Result<BuildResponse>.Failure(access.ErrorType, access.Error!);

        var build = await _buildRepository.GetByIdForProjectAsync(projectId, buildId, cancellationToken);
        if (build is null)
            return Result<BuildResponse>.Failure(ErrorType.NotFound, "Build not found.");

        try
        {
            build.UpdateMetadata(request.Version, request.Name, request.Branch, request.CommitSha, request.EngineVersion, request.Changelog);
        }
        catch (ArgumentException ex)
        {
            return Result<BuildResponse>.Failure(ErrorType.Validation, ex.Message);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<BuildResponse>.Success(ToResponse(build));
    }

    public async Task<Result<BuildResponse>> ArchiveAsync(Guid projectId, Guid buildId, Guid userId, CancellationToken cancellationToken)
    {
        var access = await AuthorizeProjectAsync(projectId, userId, cancellationToken);
        if (!access.IsSuccess)
            return Result<BuildResponse>.Failure(access.ErrorType, access.Error!);

        var build = await _buildRepository.GetByIdForProjectAsync(projectId, buildId, cancellationToken);
        if (build is null)
            return Result<BuildResponse>.Failure(ErrorType.NotFound, "Build not found.");

        build.Archive();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<BuildResponse>.Success(ToResponse(build));
    }

    public async Task<Result<BuildResponse>> RestoreAsync(Guid projectId, Guid buildId, Guid userId, CancellationToken cancellationToken)
    {
        var access = await AuthorizeProjectAsync(projectId, userId, cancellationToken);
        if (!access.IsSuccess)
            return Result<BuildResponse>.Failure(access.ErrorType, access.Error!);

        var build = await _buildRepository.GetByIdForProjectAsync(projectId, buildId, cancellationToken);
        if (build is null)
            return Result<BuildResponse>.Failure(ErrorType.NotFound, "Build not found.");

        build.Restore();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<BuildResponse>.Success(ToResponse(build));
    }

    private async Task<Result<bool>> AuthorizeProjectAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
            return Result<bool>.Failure(ErrorType.NotFound, "Project not found.");

        var membership = await _organizationRepository.GetMembershipAsync(project.OrganizationId, userId, cancellationToken);
        if (membership is null)
            return Result<bool>.Failure(ErrorType.Forbidden, "You are not a member of this project's organization.");

        return Result<bool>.Success(true);
    }

    private static BuildStatusFilter ParseStatus(string? status) => status?.ToLowerInvariant() switch
    {
        "archived" => BuildStatusFilter.Archived,
        "all" => BuildStatusFilter.All,
        _ => BuildStatusFilter.Active
    };

    private static BuildResponse ToResponse(Build build) => new(
        build.Id,
        build.ProjectId,
        build.Name,
        build.Version,
        build.BuildNumber,
        build.Platform,
        build.Configuration,
        new BuildSourceResponse(build.Branch, build.CommitSha),
        build.EngineVersion,
        build.Changelog,
        build.ArchivedAt,
        new BuildCreatedByResponse(build.CreatedByUserId, build.CreatedByDisplayName),
        build.CreatedAt,
        build.UpdatedAt);
}
