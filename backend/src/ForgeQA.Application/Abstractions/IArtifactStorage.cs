namespace ForgeQA.Application.Abstractions;

public record StorageMultipartUpload(string ProviderUploadId);
public record StorageCompletedPart(int PartNumber, string ETag);
public record StorageObjectMetadata(long SizeBytes, string? Sha256);

/// <summary>Adds multipart upload, needed for build artifacts that can exceed a single PUT's practical size.</summary>
public interface IArtifactStorage : IObjectStorage
{
    Task<StorageMultipartUpload> CreateMultipartUploadAsync(string objectKey, string contentType, CancellationToken cancellationToken);
    string CreateUploadPartUrl(string objectKey, string providerUploadId, int partNumber, TimeSpan lifetime);
    Task CompleteMultipartUploadAsync(string objectKey, string providerUploadId, IReadOnlyList<StorageCompletedPart> parts, CancellationToken cancellationToken);
    Task AbortMultipartUploadAsync(string objectKey, string providerUploadId, CancellationToken cancellationToken);
}
