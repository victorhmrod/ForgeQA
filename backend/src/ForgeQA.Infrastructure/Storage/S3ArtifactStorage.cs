using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using ForgeQA.Application.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;

namespace ForgeQA.Infrastructure.Storage;

public sealed class S3ArtifactStorage : IArtifactStorage, IDisposable
{
    private readonly AmazonS3Client _serviceClient;
    private readonly AmazonS3Client _signingClient;
    private readonly S3StorageOptions _options;

    public S3ArtifactStorage(IOptions<S3StorageOptions> options)
    {
        _options = options.Value;
        var credentials = new BasicAWSCredentials(_options.AccessKey, _options.SecretKey);

        _serviceClient = new AmazonS3Client(credentials, CreateConfig(_options.Endpoint));
        _signingClient = new AmazonS3Client(credentials, CreateConfig(_options.PublicEndpoint));
    }

    public async Task<StorageMultipartUpload> CreateMultipartUploadAsync(string objectKey, string contentType, CancellationToken cancellationToken)
    {
        var response = await _serviceClient.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
        {
            BucketName = _options.Bucket,
            Key = objectKey,
            ContentType = contentType
        }, cancellationToken);

        return new StorageMultipartUpload(response.UploadId);
    }

    public string CreatePutUrl(string objectKey, string contentType, TimeSpan lifetime)
    {
        return _signingClient.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = DateTime.UtcNow.Add(lifetime)
        });
    }

    public string CreateUploadPartUrl(string objectKey, string providerUploadId, int partNumber, TimeSpan lifetime)
    {
        return _signingClient.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            UploadId = providerUploadId,
            PartNumber = partNumber,
            Expires = DateTime.UtcNow.Add(lifetime)
        });
    }

    public async Task CompleteMultipartUploadAsync(string objectKey, string providerUploadId, IReadOnlyList<StorageCompletedPart> parts, CancellationToken cancellationToken)
    {
        await _serviceClient.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest
        {
            BucketName = _options.Bucket,
            Key = objectKey,
            UploadId = providerUploadId,
            PartETags = parts.Select(part => new PartETag
            {
                PartNumber = part.PartNumber,
                ETag = part.ETag
            }).ToList()
        }, cancellationToken);
    }

    public async Task AbortMultipartUploadAsync(string objectKey, string providerUploadId, CancellationToken cancellationToken)
    {
        await _serviceClient.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
        {
            BucketName = _options.Bucket,
            Key = objectKey,
            UploadId = providerUploadId
        }, cancellationToken);
    }

    public async Task<StorageObjectMetadata?> GetMetadataAsync(string objectKey, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _serviceClient.GetObjectMetadataAsync(_options.Bucket, objectKey, cancellationToken);
            return new StorageObjectMetadata(response.ContentLength, null);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public string CreateDownloadUrl(string objectKey, string fileName, string contentType, TimeSpan lifetime)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(lifetime)
        };
        request.ResponseHeaderOverrides.ContentType = contentType;
        request.ResponseHeaderOverrides.ContentDisposition = $"attachment; filename=\"{SanitizeHeaderFileName(fileName)}\"";
        return _signingClient.GetPreSignedURL(request);
    }

    public Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken) =>
        _serviceClient.DeleteObjectAsync(_options.Bucket, objectKey, cancellationToken);

    public void Dispose()
    {
        _serviceClient.Dispose();
        _signingClient.Dispose();
    }

    private AmazonS3Config CreateConfig(string endpoint) => new()
    {
        ServiceURL = endpoint,
        ForcePathStyle = _options.ForcePathStyle,
        AuthenticationRegion = _options.Region
    };

    private static string SanitizeHeaderFileName(string fileName) =>
        fileName.Replace("\"", "'").Replace("\r", string.Empty).Replace("\n", string.Empty);
}
