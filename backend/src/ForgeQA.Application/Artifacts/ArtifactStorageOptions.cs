namespace ForgeQA.Application.Artifacts;

public sealed class ArtifactStorageOptions
{
    public const string SectionName = "ArtifactStorage";

    public long MultipartPartSizeBytes { get; set; } = 67_108_864;
    public long MaxArtifactSizeBytes { get; set; } = 107_374_182_400;
    public int UploadSessionLifetimeMinutes { get; set; } = 60;
    public int DownloadUrlLifetimeMinutes { get; set; } = 10;
    public int MaxPresignedPartsPerRequest { get; set; } = 50;
}
