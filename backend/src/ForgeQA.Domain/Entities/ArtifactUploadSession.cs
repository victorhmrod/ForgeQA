using ForgeQA.Domain.Common;

namespace ForgeQA.Domain.Entities;

public class ArtifactUploadSession : Entity
{
    public const int ProviderUploadIdMaxLength = 512;

    public Guid ArtifactId { get; private set; }
    public string ProviderUploadId { get; private set; } = null!;
    public long PartSizeBytes { get; private set; }
    public int ExpectedParts { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? AbortedAt { get; private set; }

    public bool IsCompleted => CompletedAt is not null;
    public bool IsAborted => AbortedAt is not null;
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    private ArtifactUploadSession() { }

    public ArtifactUploadSession(Guid artifactId, string providerUploadId, long partSizeBytes, int expectedParts, DateTime expiresAt)
    {
        if (artifactId == Guid.Empty)
            throw new ArgumentException("ArtifactId is required.", nameof(artifactId));
        if (string.IsNullOrWhiteSpace(providerUploadId))
            throw new ArgumentException("Provider upload ID is required.", nameof(providerUploadId));
        if (providerUploadId.Length > ProviderUploadIdMaxLength)
            throw new ArgumentException($"Provider upload ID must be at most {ProviderUploadIdMaxLength} characters.", nameof(providerUploadId));
        if (partSizeBytes <= 0)
            throw new ArgumentException("Part size must be greater than zero.", nameof(partSizeBytes));
        if (expectedParts <= 0)
            throw new ArgumentException("Expected part count must be greater than zero.", nameof(expectedParts));
        if (expiresAt <= DateTime.UtcNow)
            throw new ArgumentException("Upload session expiration must be in the future.", nameof(expiresAt));

        ArtifactId = artifactId;
        ProviderUploadId = providerUploadId.Trim();
        PartSizeBytes = partSizeBytes;
        ExpectedParts = expectedParts;
        ExpiresAt = expiresAt;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkCompleted()
    {
        if (IsAborted)
            throw new InvalidOperationException("An aborted upload session cannot be completed.");
        if (IsCompleted)
            return;

        CompletedAt = DateTime.UtcNow;
    }

    public void MarkAborted()
    {
        if (IsCompleted)
            throw new InvalidOperationException("A completed upload session cannot be aborted.");
        if (IsAborted)
            return;

        AbortedAt = DateTime.UtcNow;
    }
}
