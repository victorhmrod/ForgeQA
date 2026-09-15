using ForgeQA.Application.Abstractions;
using System.Collections.Concurrent;

namespace ForgeQA.IntegrationTests;

public sealed class FakeArtifactStorage : IArtifactStorage
{
    private readonly ConcurrentDictionary<string, StorageObjectMetadata> _objects = new();

    public Task<StorageMultipartUpload> CreateMultipartUploadAsync(string objectKey, string contentType, CancellationToken cancellationToken) =>
        Task.FromResult(new StorageMultipartUpload($"upload-{Guid.NewGuid():N}"));

    public string CreateUploadPartUrl(string objectKey, string providerUploadId, int partNumber, TimeSpan lifetime) =>
        $"https://storage.test/upload/{Uri.EscapeDataString(objectKey)}?uploadId={providerUploadId}&partNumber={partNumber}";

    public Task CompleteMultipartUploadAsync(
        string objectKey,
        string providerUploadId,
        IReadOnlyList<StorageCompletedPart> parts,
        CancellationToken cancellationToken)
    {
        _objects[objectKey] = new StorageObjectMetadata(1024, null);
        return Task.CompletedTask;
    }

    public Task AbortMultipartUploadAsync(string objectKey, string providerUploadId, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<StorageObjectMetadata?> GetMetadataAsync(string objectKey, CancellationToken cancellationToken)
    {
        _objects.TryGetValue(objectKey, out var metadata);
        return Task.FromResult(metadata);
    }

    public string CreateDownloadUrl(string objectKey, string fileName, string contentType, TimeSpan lifetime) =>
        $"https://storage.test/download/{Uri.EscapeDataString(objectKey)}";

    public Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken)
    {
        _objects.TryRemove(objectKey, out _);
        return Task.CompletedTask;
    }
}
