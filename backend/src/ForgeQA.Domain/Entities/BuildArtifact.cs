using ForgeQA.Domain.Common;
using ForgeQA.Domain.Enums;

namespace ForgeQA.Domain.Entities;

public class BuildArtifact : Entity
{
    public const int FileNameMaxLength = 255;
    public const int DisplayNameMaxLength = 200;
    public const int ContentTypeMaxLength = 200;
    public const int Sha256Length = 64;
    public const int StorageObjectKeyMaxLength = 1024;

    public Guid BuildId { get; private set; }
    public string FileName { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public ArtifactType ArtifactType { get; private set; }
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public string? Sha256 { get; private set; }
    public ArtifactStatus Status { get; private set; }
    public string StorageObjectKey { get; private set; } = null!;
    public Guid CreatedByUserId { get; private set; }
    public string CreatedByDisplayName { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    private BuildArtifact() { }

    public BuildArtifact(
        Guid buildId,
        string fileName,
        string displayName,
        ArtifactType artifactType,
        string? contentType,
        long sizeBytes,
        string storageObjectKey,
        Guid createdByUserId,
        string createdByDisplayName,
        string? sha256 = null)
    {
        if (buildId == Guid.Empty)
            throw new ArgumentException("BuildId is required.", nameof(buildId));
        if (createdByUserId == Guid.Empty)
            throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        if (sizeBytes <= 0)
            throw new ArgumentException("Artifact size must be greater than zero.", nameof(sizeBytes));

        BuildId = buildId;
        ArtifactType = artifactType;
        SizeBytes = sizeBytes;
        CreatedByUserId = createdByUserId;
        CreatedByDisplayName = string.IsNullOrWhiteSpace(createdByDisplayName) ? "Unknown" : createdByDisplayName.Trim();
        SetFileName(fileName);
        SetDisplayName(displayName);
        SetContentType(contentType);
        SetSha256(sha256);
        SetStorageObjectKey(storageObjectKey);

        Status = ArtifactStatus.PENDING;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void MarkUploading()
    {
        EnsureNotDeleted();
        if (Status is ArtifactStatus.READY or ArtifactStatus.VERIFYING)
            throw new InvalidOperationException("A completed artifact cannot return to uploading state.");

        Status = ArtifactStatus.UPLOADING;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkVerifying()
    {
        EnsureNotDeleted();
        if (Status is not ArtifactStatus.UPLOADING)
            throw new InvalidOperationException("Only an uploading artifact can be verified.");

        Status = ArtifactStatus.VERIFYING;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkReady()
    {
        EnsureNotDeleted();
        if (Status is not ArtifactStatus.VERIFYING)
            throw new InvalidOperationException("Only a verifying artifact can become ready.");

        Status = ArtifactStatus.READY;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = CompletedAt.Value;
    }

    public void MarkFailed()
    {
        EnsureNotDeleted();
        if (Status is ArtifactStatus.READY)
            throw new InvalidOperationException("A ready artifact cannot be marked failed.");

        Status = ArtifactStatus.FAILED;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDeleted()
    {
        if (DeletedAt is not null)
            return;

        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DeletedAt.Value;
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new InvalidOperationException("Deleted artifacts cannot change state.");
    }

    private void SetFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.", nameof(fileName));
        if (fileName.Length > FileNameMaxLength)
            throw new ArgumentException($"File name must be at most {FileNameMaxLength} characters.", nameof(fileName));

        FileName = Path.GetFileName(fileName.Trim());
        if (string.IsNullOrWhiteSpace(FileName))
            throw new ArgumentException("File name is invalid.", nameof(fileName));
    }

    private void SetDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));
        if (displayName.Length > DisplayNameMaxLength)
            throw new ArgumentException($"Display name must be at most {DisplayNameMaxLength} characters.", nameof(displayName));

        DisplayName = displayName.Trim();
    }

    private void SetContentType(string? contentType)
    {
        var normalized = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
        if (normalized.Length > ContentTypeMaxLength)
            throw new ArgumentException($"Content type must be at most {ContentTypeMaxLength} characters.", nameof(contentType));

        ContentType = normalized;
    }

    private void SetSha256(string? sha256)
    {
        if (string.IsNullOrWhiteSpace(sha256))
        {
            Sha256 = null;
            return;
        }

        var normalized = sha256.Trim().ToLowerInvariant();
        if (normalized.Length != Sha256Length || normalized.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("SHA-256 must be a 64-character hexadecimal string.", nameof(sha256));

        Sha256 = normalized;
    }

    private void SetStorageObjectKey(string storageObjectKey)
    {
        if (string.IsNullOrWhiteSpace(storageObjectKey))
            throw new ArgumentException("Storage object key is required.", nameof(storageObjectKey));
        if (storageObjectKey.Length > StorageObjectKeyMaxLength)
            throw new ArgumentException($"Storage object key must be at most {StorageObjectKeyMaxLength} characters.", nameof(storageObjectKey));

        StorageObjectKey = storageObjectKey.Trim();
    }
}
