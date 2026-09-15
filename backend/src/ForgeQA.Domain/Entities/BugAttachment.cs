using ForgeQA.Domain.Common;
using ForgeQA.Domain.Enums;

namespace ForgeQA.Domain.Entities;

public class BugAttachment : Entity
{
    public const int FileNameMaxLength = 255;
    public const int ContentTypeMaxLength = 200;
    public const int StorageObjectKeyMaxLength = 1024;

    public Guid BugReportId { get; private set; }
    public BugAttachmentType Type { get; private set; }
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public string StorageObjectKey { get; private set; } = null!;
    public BugAttachmentStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    private BugAttachment() { }

    public BugAttachment(
        Guid bugReportId,
        BugAttachmentType type,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageObjectKey)
    {
        if (bugReportId == Guid.Empty)
            throw new ArgumentException("BugReportId is required.", nameof(bugReportId));
        if (sizeBytes <= 0)
            throw new ArgumentException("Attachment size must be greater than zero.", nameof(sizeBytes));

        BugReportId = bugReportId;
        Type = type;
        SizeBytes = sizeBytes;

        SetFileName(fileName);
        SetContentType(contentType);
        SetStorageObjectKey(storageObjectKey);

        Status = BugAttachmentStatus.PENDING;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkReady()
    {
        if (IsDeleted)
            throw new InvalidOperationException("A deleted attachment cannot change state.");
        if (Status is BugAttachmentStatus.FAILED)
            throw new InvalidOperationException("A failed attachment cannot become ready.");

        Status = BugAttachmentStatus.READY;
    }

    public void MarkFailed()
    {
        if (IsDeleted)
            return;

        Status = BugAttachmentStatus.FAILED;
    }

    public void SoftDelete()
    {
        if (IsDeleted)
            return;

        DeletedAt = DateTime.UtcNow;
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

    private void SetContentType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type is required.", nameof(contentType));
        if (contentType.Length > ContentTypeMaxLength)
            throw new ArgumentException($"Content type must be at most {ContentTypeMaxLength} characters.", nameof(contentType));

        ContentType = contentType.Trim();
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
