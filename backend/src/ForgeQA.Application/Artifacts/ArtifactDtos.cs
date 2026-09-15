using ForgeQA.Domain.Enums;

namespace ForgeQA.Application.Artifacts;

public record InitiateArtifactUploadRequest(
    string FileName,
    string DisplayName,
    ArtifactType ArtifactType,
    string? ContentType,
    long SizeBytes,
    string? Sha256);

public record InitiateArtifactUploadResponse(
    Guid ArtifactId,
    Guid UploadSessionId,
    long PartSizeBytes,
    int PartCount,
    DateTime ExpiresAt);

public record RequestUploadPartsRequest(Guid UploadSessionId, IReadOnlyList<int> PartNumbers);
public record UploadPartUrlResponse(int PartNumber, string Url);
public record RequestUploadPartsResponse(IReadOnlyList<UploadPartUrlResponse> Parts);

public record CompletedUploadPartRequest(int PartNumber, string ETag);
public record CompleteArtifactUploadRequest(Guid UploadSessionId, IReadOnlyList<CompletedUploadPartRequest> Parts);
public record AbortArtifactUploadRequest(Guid UploadSessionId);

public record ArtifactCreatedByResponse(Guid Id, string Name);
public record BuildArtifactResponse(
    Guid Id,
    Guid BuildId,
    string FileName,
    string DisplayName,
    ArtifactType ArtifactType,
    string ContentType,
    long SizeBytes,
    string? Sha256,
    ArtifactStatus Status,
    ArtifactCreatedByResponse CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? CompletedAt,
    DateTime? DeletedAt);

public record ArtifactDownloadResponse(string Url, DateTime ExpiresAt);
