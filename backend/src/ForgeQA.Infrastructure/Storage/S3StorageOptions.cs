namespace ForgeQA.Infrastructure.Storage;

public sealed class S3StorageOptions
{
    public string Endpoint { get; set; } = null!;
    public string PublicEndpoint { get; set; } = null!;
    public string Region { get; set; } = "us-east-1";
    public string Bucket { get; set; } = "forgeqa-artifacts";
    public string AccessKey { get; set; } = null!;
    public string SecretKey { get; set; } = null!;
    public bool ForcePathStyle { get; set; } = true;
}
