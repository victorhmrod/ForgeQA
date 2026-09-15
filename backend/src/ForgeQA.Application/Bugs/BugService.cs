using ForgeQA.Application.Abstractions;
using ForgeQA.Application.Common;
using ForgeQA.Domain.Entities;
using ForgeQA.Domain.Enums;
using Microsoft.Extensions.Options;

namespace ForgeQA.Application.Bugs;

public class BugService
{
    private readonly IBugRepository _bugRepository;
    private readonly IBuildRepository _buildRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IIdentityService _identityService;
    private readonly IObjectStorage _objectStorage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly BugReportingOptions _options;

    public BugService(
        IBugRepository bugRepository,
        IBuildRepository buildRepository,
        IProjectRepository projectRepository,
        IOrganizationRepository organizationRepository,
        IIdentityService identityService,
        IObjectStorage objectStorage,
        IUnitOfWork unitOfWork,
        IOptions<BugReportingOptions> options)
    {
        _bugRepository = bugRepository;
        _buildRepository = buildRepository;
        _projectRepository = projectRepository;
        _organizationRepository = organizationRepository;
        _identityService = identityService;
        _objectStorage = objectStorage;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    public async Task<Result<BugResponse>> CreateAsync(Guid projectId, BugReportAuthor author, CreateBugRequest request, CancellationToken cancellationToken)
    {
        var access = await AuthorizeAsync(projectId, author, cancellationToken);
        if (!access.IsSuccess)
            return Result<BugResponse>.Failure(access.ErrorType, access.Error!);

        var project = access.Value!;

        // Prevents a caller from claiming a source their authentication method could not have
        // produced: a runtime key cannot fabricate a WEB report, and a dashboard user cannot
        // fabricate an UNREAL_RUNTIME one.
        if (author.IsApiKey && request.Source == BugSource.WEB)
            return Result<BugResponse>.Failure(ErrorType.Validation, "A Project API key cannot submit a WEB-sourced report.");
        if (!author.IsApiKey && request.Source == BugSource.UNREAL_RUNTIME)
            return Result<BugResponse>.Failure(ErrorType.Validation, "An UNREAL_RUNTIME report must be submitted using a Project API key.");

        Build? build = null;
        if (request.BuildId is { } buildId)
        {
            build = await _buildRepository.GetByIdForProjectAsync(projectId, buildId, cancellationToken);
            if (build is null)
                return Result<BugResponse>.Failure(ErrorType.Validation, "Build not found for this project.");
        }

        Guid? reporterUserId = null;
        string? reporterDisplayName = request.ReporterDisplayName;
        if (author.UserId is { } userId)
        {
            reporterUserId = userId;
            var user = await _identityService.GetUserByIdAsync(userId, cancellationToken);
            reporterDisplayName ??= user?.DisplayName;
        }

        var environment = ToDomainEnvironment(request.Environment);

        BugReport bug;
        try
        {
            bug = new BugReport(
                projectId,
                request.Title,
                request.Severity,
                request.Source,
                build?.Id,
                request.Description,
                request.ReproductionSteps,
                reporterUserId,
                reporterDisplayName,
                request.RuntimeSessionId,
                environment);
        }
        catch (ArgumentException ex)
        {
            return Result<BugResponse>.Failure(ErrorType.Validation, ex.Message);
        }

        _bugRepository.Add(bug);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<BugResponse>.Success(await ToResponseAsync(bug, project, cancellationToken));
    }

    public async Task<Result<PagedResult<BugListItemResponse>>> GetForProjectAsync(Guid projectId, Guid userId, ListBugsRequest request, CancellationToken cancellationToken)
    {
        var access = await AuthorizeForUserAsync(projectId, userId, cancellationToken);
        if (!access.IsSuccess)
            return Result<PagedResult<BugListItemResponse>>.Failure(access.ErrorType, access.Error!);

        const int defaultPageSize = 20;
        const int maxPageSize = 100;
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? defaultPageSize : Math.Min(request.PageSize, maxPageSize);

        var query = new BugQuery(projectId, page, pageSize, request.Status, request.Severity, request.BuildId, request.Source, request.Search);
        var (items, totalCount) = await _bugRepository.QueryAsync(query, cancellationToken);

        var buildIds = items.Where(b => b.BuildId is not null).Select(b => b.BuildId!.Value).Distinct().ToList();
        var builds = buildIds.Count == 0
            ? new List<Build>()
            : await _buildRepository.GetByIdsAsync(buildIds, cancellationToken);
        var buildsById = builds.ToDictionary(b => b.Id);

        var listItems = items.Select(bug => new BugListItemResponse(
            bug.Id,
            bug.ProjectId,
            bug.Title,
            bug.Severity,
            bug.Status,
            bug.Source,
            bug.BuildId is not null && buildsById.TryGetValue(bug.BuildId.Value, out var build) ? ToBuildSummary(build) : null,
            new BugReporterResponse(bug.ReporterUserId, bug.ReporterDisplayName),
            bug.CreatedAt)).ToList();

        return Result<PagedResult<BugListItemResponse>>.Success(new PagedResult<BugListItemResponse>(listItems, page, pageSize, totalCount));
    }

    public async Task<Result<BugResponse>> GetByIdAsync(Guid projectId, Guid bugId, Guid userId, CancellationToken cancellationToken)
    {
        var access = await AuthorizeForUserAsync(projectId, userId, cancellationToken);
        if (!access.IsSuccess)
            return Result<BugResponse>.Failure(access.ErrorType, access.Error!);

        var bug = await _bugRepository.GetByIdForProjectAsync(projectId, bugId, cancellationToken, includeAttachments: true);
        if (bug is null)
            return Result<BugResponse>.Failure(ErrorType.NotFound, "Bug report not found.");

        return Result<BugResponse>.Success(await ToResponseAsync(bug, access.Value!, cancellationToken));
    }

    public async Task<Result<BugResponse>> UpdateAsync(Guid projectId, Guid bugId, Guid userId, UpdateBugRequest request, CancellationToken cancellationToken)
    {
        var access = await AuthorizeForUserAsync(projectId, userId, cancellationToken);
        if (!access.IsSuccess)
            return Result<BugResponse>.Failure(access.ErrorType, access.Error!);

        var bug = await _bugRepository.GetByIdForProjectAsync(projectId, bugId, cancellationToken, includeAttachments: true);
        if (bug is null)
            return Result<BugResponse>.Failure(ErrorType.NotFound, "Bug report not found.");

        try
        {
            bug.UpdateDetails(request.Title, request.Description, request.ReproductionSteps, request.Severity);
        }
        catch (ArgumentException ex)
        {
            return Result<BugResponse>.Failure(ErrorType.Validation, ex.Message);
        }

        bug.ChangeStatus(request.Status);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<BugResponse>.Success(await ToResponseAsync(bug, access.Value!, cancellationToken));
    }

    public async Task<Result<InitiateBugAttachmentResponse>> InitiateAttachmentAsync(
        Guid projectId, Guid bugId, BugReportAuthor author, InitiateBugAttachmentRequest request, CancellationToken cancellationToken)
    {
        var access = await AuthorizeAsync(projectId, author, cancellationToken);
        if (!access.IsSuccess)
            return Result<InitiateBugAttachmentResponse>.Failure(access.ErrorType, access.Error!);

        var bug = await _bugRepository.GetByIdForProjectAsync(projectId, bugId, cancellationToken);
        if (bug is null)
            return Result<InitiateBugAttachmentResponse>.Failure(ErrorType.NotFound, "Bug report not found.");

        if (request.SizeBytes <= 0)
            return Result<InitiateBugAttachmentResponse>.Failure(ErrorType.Validation, "Attachment size must be greater than zero.");
        if (request.Type == BugAttachmentType.SCREENSHOT && request.SizeBytes > _options.MaxScreenshotSizeBytes)
            return Result<InitiateBugAttachmentResponse>.Failure(ErrorType.Validation, "Screenshot exceeds the configured maximum size.");
        if (!IsAllowedContentType(request.Type, request.ContentType))
            return Result<InitiateBugAttachmentResponse>.Failure(ErrorType.Validation, $"Content type '{request.ContentType}' is not allowed for this attachment type.");

        var storageObjectKey =
            $"organizations/{access.Value!.OrganizationId:N}/projects/{projectId:N}/bugs/{bugId:N}/attachments/{Guid.NewGuid():N}/{SanitizeFileName(request.FileName)}";

        BugAttachment attachment;
        try
        {
            attachment = new BugAttachment(bugId, request.Type, request.FileName, request.ContentType, request.SizeBytes, storageObjectKey);
        }
        catch (ArgumentException ex)
        {
            return Result<InitiateBugAttachmentResponse>.Failure(ErrorType.Validation, ex.Message);
        }

        _bugRepository.Add(attachment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var lifetime = TimeSpan.FromMinutes(_options.AttachmentUploadUrlLifetimeMinutes);
        var uploadUrl = _objectStorage.CreatePutUrl(storageObjectKey, attachment.ContentType, lifetime);

        return Result<InitiateBugAttachmentResponse>.Success(new InitiateBugAttachmentResponse(attachment.Id, uploadUrl, DateTime.UtcNow.Add(lifetime)));
    }

    public async Task<Result<BugAttachmentResponse>> CompleteAttachmentAsync(
        Guid projectId, Guid bugId, Guid attachmentId, BugReportAuthor author, CancellationToken cancellationToken)
    {
        var access = await AuthorizeAsync(projectId, author, cancellationToken);
        if (!access.IsSuccess)
            return Result<BugAttachmentResponse>.Failure(access.ErrorType, access.Error!);

        var bug = await _bugRepository.GetByIdForProjectAsync(projectId, bugId, cancellationToken);
        if (bug is null)
            return Result<BugAttachmentResponse>.Failure(ErrorType.NotFound, "Bug report not found.");

        var attachment = await _bugRepository.GetAttachmentAsync(bugId, attachmentId, cancellationToken);
        if (attachment is null)
            return Result<BugAttachmentResponse>.Failure(ErrorType.NotFound, "Attachment not found.");

        if (attachment.Status == BugAttachmentStatus.READY)
            return Result<BugAttachmentResponse>.Success(ToAttachmentResponse(attachment));

        var metadata = await _objectStorage.GetMetadataAsync(attachment.StorageObjectKey, cancellationToken);
        if (metadata is null)
        {
            attachment.MarkFailed();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<BugAttachmentResponse>.Failure(ErrorType.Conflict, "The uploaded object could not be found in storage.");
        }

        if (metadata.SizeBytes != attachment.SizeBytes)
        {
            attachment.MarkFailed();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<BugAttachmentResponse>.Failure(ErrorType.Conflict, "Uploaded object size does not match the registered attachment size.");
        }

        attachment.MarkReady();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<BugAttachmentResponse>.Success(ToAttachmentResponse(attachment));
    }

    public async Task<Result<BugAttachmentDownloadResponse>> CreateAttachmentDownloadAsync(
        Guid projectId, Guid bugId, Guid attachmentId, Guid userId, CancellationToken cancellationToken)
    {
        var access = await AuthorizeForUserAsync(projectId, userId, cancellationToken);
        if (!access.IsSuccess)
            return Result<BugAttachmentDownloadResponse>.Failure(access.ErrorType, access.Error!);

        var bug = await _bugRepository.GetByIdForProjectAsync(projectId, bugId, cancellationToken);
        if (bug is null)
            return Result<BugAttachmentDownloadResponse>.Failure(ErrorType.NotFound, "Bug report not found.");

        var attachment = await _bugRepository.GetAttachmentAsync(bugId, attachmentId, cancellationToken);
        if (attachment is null || attachment.IsDeleted)
            return Result<BugAttachmentDownloadResponse>.Failure(ErrorType.NotFound, "Attachment not found.");

        if (attachment.Status != BugAttachmentStatus.READY)
            return Result<BugAttachmentDownloadResponse>.Failure(ErrorType.Conflict, "Only a ready attachment can be downloaded.");

        var lifetime = TimeSpan.FromMinutes(_options.AttachmentDownloadUrlLifetimeMinutes);
        var url = _objectStorage.CreateDownloadUrl(attachment.StorageObjectKey, attachment.FileName, attachment.ContentType, lifetime);
        return Result<BugAttachmentDownloadResponse>.Success(new BugAttachmentDownloadResponse(url, DateTime.UtcNow.Add(lifetime)));
    }

    private async Task<Result<Project>> AuthorizeForUserAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
            return Result<Project>.Failure(ErrorType.NotFound, "Project not found.");

        var membership = await _organizationRepository.GetMembershipAsync(project.OrganizationId, userId, cancellationToken);
        if (membership is null)
            return Result<Project>.Failure(ErrorType.Forbidden, "You are not a member of this project's organization.");

        return Result<Project>.Success(project);
    }

    /// <summary>
    /// Authorizes either a dashboard user (project membership, same as everywhere else) or a
    /// Project API key (must be bound to exactly this Project — a key can never act outside the
    /// single Project it was issued for, regardless of what the caller claims in the route).
    /// </summary>
    private async Task<Result<Project>> AuthorizeAsync(Guid projectId, BugReportAuthor author, CancellationToken cancellationToken)
    {
        if (author.IsApiKey)
        {
            if (author.ApiKeyProjectId != projectId)
                return Result<Project>.Failure(ErrorType.Forbidden, "This Project API key is not authorized for this project.");

            var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken);
            return project is null
                ? Result<Project>.Failure(ErrorType.NotFound, "Project not found.")
                : Result<Project>.Success(project);
        }

        return await AuthorizeForUserAsync(projectId, author.UserId!.Value, cancellationToken);
    }

    private async Task<BugResponse> ToResponseAsync(BugReport bug, Project project, CancellationToken cancellationToken)
    {
        BugBuildSummaryResponse? buildSummary = null;
        if (bug.BuildId is { } buildId)
        {
            var build = await _buildRepository.GetByIdForProjectAsync(project.Id, buildId, cancellationToken);
            if (build is not null)
                buildSummary = ToBuildSummary(build);
        }

        return new BugResponse(
            bug.Id,
            bug.ProjectId,
            buildSummary,
            bug.Title,
            bug.Description,
            bug.ReproductionSteps,
            bug.Severity,
            bug.Status,
            bug.Source,
            bug.RuntimeSessionId,
            ToEnvironmentDto(bug.Environment),
            new BugReporterResponse(bug.ReporterUserId, bug.ReporterDisplayName),
            bug.Attachments.Where(a => !a.IsDeleted).Select(ToAttachmentResponse).ToList(),
            bug.CreatedAt,
            bug.UpdatedAt,
            bug.ResolvedAt,
            bug.ClosedAt);
    }

    private static bool IsAllowedContentType(BugAttachmentType type, string contentType) => type switch
    {
        BugAttachmentType.SCREENSHOT => contentType is "image/png" or "image/jpeg",
        BugAttachmentType.LOG => contentType is "text/plain" or "application/octet-stream",
        _ => true
    };

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Trim());
        if (string.IsNullOrWhiteSpace(name))
            return "attachment.bin";

        foreach (var invalid in Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\' }).Distinct())
            name = name.Replace(invalid, '_');

        return name;
    }

    private static BugEnvironment? ToDomainEnvironment(BugEnvironmentDto? dto) => dto is null
        ? null
        : new BugEnvironment(dto.MapName, dto.GameMode, dto.Platform, dto.EngineVersion, dto.OsVersion, dto.Cpu, dto.Gpu, dto.MemoryBytes, dto.Locale);

    private static BugEnvironmentDto ToEnvironmentDto(BugEnvironment environment) => new(
        environment.MapName, environment.GameMode, environment.Platform, environment.EngineVersion,
        environment.OsVersion, environment.Cpu, environment.Gpu, environment.MemoryBytes, environment.Locale);

    private static BugBuildSummaryResponse ToBuildSummary(Build build) => new(build.Id, build.Version, build.BuildNumber, build.Platform, build.Configuration);

    private static BugAttachmentResponse ToAttachmentResponse(BugAttachment attachment) => new(
        attachment.Id, attachment.Type, attachment.FileName, attachment.ContentType, attachment.SizeBytes, attachment.Status, attachment.CreatedAt);
}
