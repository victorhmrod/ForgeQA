namespace ForgeQA.Application.Abstractions;

public record StorageMultipartUpload(string ProviderUploadId);
public record StorageCompletedPart(int PartNumber, string ETag);
public record StorageObjectMetadata(long SizeBytes, string? Sha256);

public interface IArtifactStorage
{
    Task<StorageMultipartUpload> CreateMultipartUploadAsync(string objectKey, string contentType, CancellationToken cancellationToken);
    string CreateUploadPartUrl(string objectKey, string providerUploadId, int partNumber, TimeSpan lifetime);
    Task CompleteMultipartUploadAsync(string objectKey, string providerUploadId, IReadOnlyList<StorageCompletedPart> parts, CancellationToken cancellationToken);
    Task AbortMultipartUploadAsync(string objectKey, string providerUploadId, CancellationToken cancellationToken);
    Task<StorageObjectMetadata?> GetMetadataAsync(string objectKey, CancellationToken cancellationToken);
    string CreateDownloadUrl(string objectKey, string fileName, string contentType, TimeSpan lifetime);
    Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken);
}
