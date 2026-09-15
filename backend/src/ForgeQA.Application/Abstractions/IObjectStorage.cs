namespace ForgeQA.Application.Abstractions;

/// <summary>
/// The generic S3-compatible object-storage primitive introduced in M2 for Build Artifacts and
/// reused, unchanged, for Bug Attachments in M4. <see cref="IArtifactStorage"/> extends this with
/// the multipart-upload operations large build artifacts need; small objects like screenshots use
/// only what's here — a single presigned PUT is enough, so they never need multipart machinery.
/// </summary>
public interface IObjectStorage
{
    /// <summary>A short-lived presigned URL the caller can PUT the object's bytes to directly.</summary>
    string CreatePutUrl(string objectKey, string contentType, TimeSpan lifetime);

    Task<StorageObjectMetadata?> GetMetadataAsync(string objectKey, CancellationToken cancellationToken);
    string CreateDownloadUrl(string objectKey, string fileName, string contentType, TimeSpan lifetime);
    Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken);
}
