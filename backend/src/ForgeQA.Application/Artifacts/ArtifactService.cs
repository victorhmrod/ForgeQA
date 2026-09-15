using ForgeQA.Application.Abstractions;
using ForgeQA.Application.Common;
using ForgeQA.Domain.Entities;
using ForgeQA.Domain.Enums;
using Microsoft.Extensions.Options;

namespace ForgeQA.Application.Artifacts;

public class ArtifactService
{
    private const int MaxS3Parts = 10_000;

    private readonly IArtifactRepository _artifactRepository;
    private readonly IArtifactStorage _artifactStorage;
    private readonly IBuildRepository _buildRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IIdentityService _identityService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ArtifactStorageOptions _options;

    public ArtifactService(
        IArtifactRepository artifactRepository,
        IArtifactStorage artifactStorage,
        IBuildRepository buildRepository,
        IProjectRepository projectRepository,
        IOrganizationRepository organizationRepository,
        IIdentityService identityService,
        IUnitOfWork unitOfWork,
        IOptions<ArtifactStorageOptions> options)
    {
        _artifactRepository = artifactRepository;
        _artifactStorage = artifactStorage;
        _buildRepository = buildRepository;
        _projectRepository = projectRepository;
        _organizationRepository = organizationRepository;
        _identityService = identityService;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    public async Task<Result<InitiateArtifactUploadResponse>> InitiateUploadAsync(
        Guid projectId,
        Guid buildId,
        Guid userId,
        InitiateArtifactUploadRequest request,
        CancellationToken cancellationToken)
    {
        var context = await AuthorizeBuildAsync(projectId, buildId, userId, cancellationToken);
        if (!context.IsSuccess)
            return Result<InitiateArtifactUploadResponse>.Failure(context.ErrorType, context.Error!);

        if (context.Value!.Build.IsArchived)
            return Result<InitiateArtifactUploadResponse>.Failure(ErrorType.Conflict, "New artifacts cannot be uploaded to an archived build.");

        if (request.SizeBytes <= 0)
            return Result<InitiateArtifactUploadResponse>.Failure(ErrorType.Validation, "Artifact size must be greater than zero.");
        if (request.SizeBytes > _options.MaxArtifactSizeBytes)
            return Result<InitiateArtifactUploadResponse>.Failure(ErrorType.Validation, "Artifact exceeds the configured maximum size.");
        if (_options.MultipartPartSizeBytes <= 0)
            return Result<InitiateArtifactUploadResponse>.Failure(ErrorType.Internal, "Artifact storage part size is not configured correctly.");

        var partCountLong = (request.SizeBytes + _options.MultipartPartSizeBytes - 1) / _options.MultipartPartSizeBytes;
        if (partCountLong <= 0 || partCountLong > MaxS3Parts)
            return Result<InitiateArtifactUploadResponse>.Failure(ErrorType.Validation, $"Artifact requires more than {MaxS3Parts} multipart upload parts.");

        var creator = await _identityService.GetUserByIdAsync(userId, cancellationToken);
        var creatorName = creator?.DisplayName ?? "Unknown";
        var safeFileName = SanitizeFileName(request.FileName);
        var storageObjectKey = $"organizations/{context.Value.Project.OrganizationId:N}/projects/{projectId:N}/builds/{buildId:N}/artifacts/{Guid.NewGuid():N}/{safeFileName}";

        BuildArtifact artifact;
        try
        {
            artifact = new BuildArtifact(
                buildId,
                request.FileName,
                request.DisplayName,
                request.ArtifactType,
                request.ContentType,
                request.SizeBytes,
                storageObjectKey,
                userId,
                creatorName,
                request.Sha256);
        }
        catch (ArgumentException ex)
        {
            return Result<InitiateArtifactUploadResponse>.Failure(ErrorType.Validation, ex.Message);
        }

        StorageMultipartUpload providerUpload;
        try
        {
            providerUpload = await _artifactStorage.CreateMultipartUploadAsync(storageObjectKey, artifact.ContentType, cancellationToken);
        }
        catch
        {
            return Result<InitiateArtifactUploadResponse>.Failure(ErrorType.Internal, "Object storage could not start the multipart upload.");
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(_options.UploadSessionLifetimeMinutes);
        var session = new ArtifactUploadSession(artifact.Id, providerUpload.ProviderUploadId, _options.MultipartPartSizeBytes, (int)partCountLong, expiresAt);
        artifact.MarkUploading();

        _artifactRepository.Add(artifact);
        _artifactRepository.Add(session);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await _artifactStorage.AbortMultipartUploadAsync(storageObjectKey, providerUpload.ProviderUploadId, CancellationToken.None);
            }
            catch
            {
                // The database remains authoritative; provider-side orphan cleanup is handled separately.
            }

            return Result<InitiateArtifactUploadResponse>.Failure(ErrorType.Internal, "The upload session could not be persisted.");
        }

        return Result<InitiateArtifactUploadResponse>.Success(new InitiateArtifactUploadResponse(
            artifact.Id,
            session.Id,
            session.PartSizeBytes,
            session.ExpectedParts,
            session.ExpiresAt));
    }

    public async Task<Result<RequestUploadPartsResponse>> GetUploadPartUrlsAsync(
        Guid projectId,
        Guid buildId,
        Guid artifactId,
        Guid userId,
        RequestUploadPartsRequest request,
        CancellationToken cancellationToken)
    {
        var context = await AuthorizeArtifactAsync(projectId, buildId, artifactId, userId, cancellationToken);
        if (!context.IsSuccess)
            return Result<RequestUploadPartsResponse>.Failure(context.ErrorType, context.Error!);

        var artifact = context.Value!;
        if (artifact.Status is not ArtifactStatus.UPLOADING)
            return Result<RequestUploadPartsResponse>.Failure(ErrorType.Conflict, "Artifact is not accepting upload parts.");

        var session = await _artifactRepository.GetUploadSessionAsync(artifactId, request.UploadSessionId, cancellationToken);
        var sessionError = ValidateActiveSession(session);
        if (sessionError is not null)
            return Result<RequestUploadPartsResponse>.Failure(sessionError.Value.Type, sessionError.Value.Message);

        if (request.PartNumbers.Count == 0)
            return Result<RequestUploadPartsResponse>.Failure(ErrorType.Validation, "At least one part number is required.");
        if (request.PartNumbers.Count > _options.MaxPresignedPartsPerRequest)
            return Result<RequestUploadPartsResponse>.Failure(ErrorType.Validation, $"At most {_options.MaxPresignedPartsPerRequest} part URLs can be requested at once.");
        if (request.PartNumbers.Distinct().Count() != request.PartNumbers.Count)
            return Result<RequestUploadPartsResponse>.Failure(ErrorType.Validation, "Part numbers must be unique.");
        if (request.PartNumbers.Any(part => part < 1 || part > session!.ExpectedParts))
            return Result<RequestUploadPartsResponse>.Failure(ErrorType.Validation, "One or more part numbers are outside the expected upload range.");

        var lifetime = session!.ExpiresAt - DateTime.UtcNow;
        var parts = request.PartNumbers
            .Order()
            .Select(partNumber => new UploadPartUrlResponse(
                partNumber,
                _artifactStorage.CreateUploadPartUrl(artifact.StorageObjectKey, session.ProviderUploadId, partNumber, lifetime)))
            .ToList();

        return Result<RequestUploadPartsResponse>.Success(new RequestUploadPartsResponse(parts));
    }

    public async Task<Result<BuildArtifactResponse>> CompleteUploadAsync(
        Guid projectId,
        Guid buildId,
        Guid artifactId,
        Guid userId,
        CompleteArtifactUploadRequest request,
        CancellationToken cancellationToken)
    {
        var context = await AuthorizeArtifactAsync(projectId, buildId, artifactId, userId, cancellationToken);
        if (!context.IsSuccess)
            return Result<BuildArtifactResponse>.Failure(context.ErrorType, context.Error!);

        var artifact = context.Value!;
        if (artifact.Status is ArtifactStatus.READY)
            return Result<BuildArtifactResponse>.Success(ToResponse(artifact));
        if (artifact.Status is not ArtifactStatus.UPLOADING)
            return Result<BuildArtifactResponse>.Failure(ErrorType.Conflict, "Artifact is not in an upload-completable state.");

        var session = await _artifactRepository.GetUploadSessionAsync(artifactId, request.UploadSessionId, cancellationToken);
        var sessionError = ValidateActiveSession(session);
        if (sessionError is not null)
            return Result<BuildArtifactResponse>.Failure(sessionError.Value.Type, sessionError.Value.Message);

        if (request.Parts.Count != session!.ExpectedParts || request.Parts.Select(p => p.PartNumber).Distinct().Count() != session.ExpectedParts)
            return Result<BuildArtifactResponse>.Failure(ErrorType.Validation, "Completion must include exactly one ETag for every expected upload part.");
        if (request.Parts.Any(part => part.PartNumber < 1 || part.PartNumber > session.ExpectedParts || string.IsNullOrWhiteSpace(part.ETag)))
            return Result<BuildArtifactResponse>.Failure(ErrorType.Validation, "Completion contains an invalid part number or ETag.");

        artifact.MarkVerifying();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await _artifactStorage.CompleteMultipartUploadAsync(
                artifact.StorageObjectKey,
                session.ProviderUploadId,
                request.Parts.Select(p => new StorageCompletedPart(p.PartNumber, p.ETag)).OrderBy(p => p.PartNumber).ToList(),
                cancellationToken);

            var metadata = await _artifactStorage.GetMetadataAsync(artifact.StorageObjectKey, cancellationToken);
            if (metadata is null || metadata.SizeBytes != artifact.SizeBytes)
            {
                artifact.MarkFailed();
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return Result<BuildArtifactResponse>.Failure(ErrorType.Conflict, "Uploaded object size does not match the registered artifact size.");
            }

            if (artifact.Sha256 is not null && metadata.Sha256 is not null && !string.Equals(artifact.Sha256, metadata.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                artifact.MarkFailed();
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return Result<BuildArtifactResponse>.Failure(ErrorType.Conflict, "Uploaded object checksum does not match the registered SHA-256.");
            }

            artifact.MarkReady();
            session.MarkCompleted();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<BuildArtifactResponse>.Success(ToResponse(artifact));
        }
        catch
        {
            artifact.MarkFailed();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<BuildArtifactResponse>.Failure(ErrorType.Internal, "Object storage could not finalize the multipart upload.");
        }
    }

    public async Task<Result<BuildArtifactResponse>> AbortUploadAsync(
        Guid projectId,
        Guid buildId,
        Guid artifactId,
        Guid userId,
        AbortArtifactUploadRequest request,
        CancellationToken cancellationToken)
    {
        var context = await AuthorizeArtifactAsync(projectId, buildId, artifactId, userId, cancellationToken);
        if (!context.IsSuccess)
            return Result<BuildArtifactResponse>.Failure(context.ErrorType, context.Error!);

        var artifact = context.Value!;
        var session = await _artifactRepository.GetUploadSessionAsync(artifactId, request.UploadSessionId, cancellationToken);
        if (session is null)
            return Result<BuildArtifactResponse>.Failure(ErrorType.NotFound, "Upload session not found.");
        if (session.IsCompleted)
            return Result<BuildArtifactResponse>.Failure(ErrorType.Conflict, "Completed uploads cannot be aborted.");
        if (session.IsAborted)
            return Result<BuildArtifactResponse>.Success(ToResponse(artifact));

        try
        {
            await _artifactStorage.AbortMultipartUploadAsync(artifact.StorageObjectKey, session.ProviderUploadId, cancellationToken);
        }
        catch
        {
            return Result<BuildArtifactResponse>.Failure(ErrorType.Internal, "Object storage could not abort the multipart upload.");
        }

        session.MarkAborted();
        if (artifact.Status is not ArtifactStatus.READY)
            artifact.MarkFailed();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<BuildArtifactResponse>.Success(ToResponse(artifact));
    }

    public async Task<Result<IReadOnlyList<BuildArtifactResponse>>> ListAsync(
        Guid projectId,
        Guid buildId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var context = await AuthorizeBuildAsync(projectId, buildId, userId, cancellationToken);
        if (!context.IsSuccess)
            return Result<IReadOnlyList<BuildArtifactResponse>>.Failure(context.ErrorType, context.Error!);

        var artifacts = await _artifactRepository.ListForBuildAsync(buildId, cancellationToken);
        return Result<IReadOnlyList<BuildArtifactResponse>>.Success(artifacts.Select(ToResponse).ToList());
    }

    public async Task<Result<BuildArtifactResponse>> GetAsync(
        Guid projectId,
        Guid buildId,
        Guid artifactId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var context = await AuthorizeArtifactAsync(projectId, buildId, artifactId, userId, cancellationToken);
        return context.IsSuccess
            ? Result<BuildArtifactResponse>.Success(ToResponse(context.Value!))
            : Result<BuildArtifactResponse>.Failure(context.ErrorType, context.Error!);
    }

    public async Task<Result<ArtifactDownloadResponse>> CreateDownloadAsync(
        Guid projectId,
        Guid buildId,
        Guid artifactId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var context = await AuthorizeArtifactAsync(projectId, buildId, artifactId, userId, cancellationToken);
        if (!context.IsSuccess)
            return Result<ArtifactDownloadResponse>.Failure(context.ErrorType, context.Error!);

        var artifact = context.Value!;
        if (artifact.Status is not ArtifactStatus.READY)
            return Result<ArtifactDownloadResponse>.Failure(ErrorType.Conflict, "Only ready artifacts can be downloaded.");

        var lifetime = TimeSpan.FromMinutes(_options.DownloadUrlLifetimeMinutes);
        var expiresAt = DateTime.UtcNow.Add(lifetime);
        var url = _artifactStorage.CreateDownloadUrl(artifact.StorageObjectKey, artifact.FileName, artifact.ContentType, lifetime);
        return Result<ArtifactDownloadResponse>.Success(new ArtifactDownloadResponse(url, expiresAt));
    }

    public async Task<Result<BuildArtifactResponse>> DeleteAsync(
        Guid projectId,
        Guid buildId,
        Guid artifactId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var buildContext = await AuthorizeBuildAsync(projectId, buildId, userId, cancellationToken);
        if (!buildContext.IsSuccess)
            return Result<BuildArtifactResponse>.Failure(buildContext.ErrorType, buildContext.Error!);

        var artifact = await _artifactRepository.GetByIdForBuildAsync(buildId, artifactId, cancellationToken, includeDeleted: true);
        if (artifact is null)
            return Result<BuildArtifactResponse>.Failure(ErrorType.NotFound, "Artifact not found.");
        if (artifact.IsDeleted)
            return Result<BuildArtifactResponse>.Success(ToResponse(artifact));

        var session = await _artifactRepository.GetActiveUploadSessionAsync(artifactId, cancellationToken);
        try
        {
            if (session is not null)
            {
                await _artifactStorage.AbortMultipartUploadAsync(artifact.StorageObjectKey, session.ProviderUploadId, cancellationToken);
                session.MarkAborted();
            }

            if (artifact.Status is ArtifactStatus.READY or ArtifactStatus.VERIFYING)
                await _artifactStorage.DeleteObjectAsync(artifact.StorageObjectKey, cancellationToken);
        }
        catch
        {
            return Result<BuildArtifactResponse>.Failure(ErrorType.Internal, "Object storage could not delete the artifact safely.");
        }

        artifact.MarkDeleted();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<BuildArtifactResponse>.Success(ToResponse(artifact));
    }

    private async Task<Result<BuildContext>> AuthorizeBuildAsync(Guid projectId, Guid buildId, Guid userId, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
            return Result<BuildContext>.Failure(ErrorType.NotFound, "Project not found.");

        var membership = await _organizationRepository.GetMembershipAsync(project.OrganizationId, userId, cancellationToken);
        if (membership is null)
            return Result<BuildContext>.Failure(ErrorType.Forbidden, "You are not a member of this project's organization.");

        var build = await _buildRepository.GetByIdForProjectAsync(projectId, buildId, cancellationToken);
        if (build is null)
            return Result<BuildContext>.Failure(ErrorType.NotFound, "Build not found.");

        return Result<BuildContext>.Success(new BuildContext(project, build));
    }

    private async Task<Result<BuildArtifact>> AuthorizeArtifactAsync(Guid projectId, Guid buildId, Guid artifactId, Guid userId, CancellationToken cancellationToken)
    {
        var buildContext = await AuthorizeBuildAsync(projectId, buildId, userId, cancellationToken);
        if (!buildContext.IsSuccess)
            return Result<BuildArtifact>.Failure(buildContext.ErrorType, buildContext.Error!);

        var artifact = await _artifactRepository.GetByIdForBuildAsync(buildId, artifactId, cancellationToken);
        if (artifact is null)
            return Result<BuildArtifact>.Failure(ErrorType.NotFound, "Artifact not found.");

        return Result<BuildArtifact>.Success(artifact);
    }

    private static (ErrorType Type, string Message)? ValidateActiveSession(ArtifactUploadSession? session)
    {
        if (session is null)
            return (ErrorType.NotFound, "Upload session not found.");
        if (session.IsCompleted)
            return (ErrorType.Conflict, "Upload session is already completed.");
        if (session.IsAborted)
            return (ErrorType.Conflict, "Upload session is aborted.");
        if (session.IsExpired)
            return (ErrorType.Conflict, "Upload session has expired.");
        return null;
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Trim());
        if (string.IsNullOrWhiteSpace(name))
            return "artifact.bin";

        foreach (var invalid in Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\' }).Distinct())
            name = name.Replace(invalid, '_');

        return name;
    }

    private static BuildArtifactResponse ToResponse(BuildArtifact artifact) => new(
        artifact.Id,
        artifact.BuildId,
        artifact.FileName,
        artifact.DisplayName,
        artifact.ArtifactType,
        artifact.ContentType,
        artifact.SizeBytes,
        artifact.Sha256,
        artifact.Status,
        new ArtifactCreatedByResponse(artifact.CreatedByUserId, artifact.CreatedByDisplayName),
        artifact.CreatedAt,
        artifact.UpdatedAt,
        artifact.CompletedAt,
        artifact.DeletedAt);

    private sealed record BuildContext(Project Project, Build Build);
}
