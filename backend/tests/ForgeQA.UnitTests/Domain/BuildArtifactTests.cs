using FluentAssertions;
using ForgeQA.Domain.Entities;
using ForgeQA.Domain.Enums;

namespace ForgeQA.UnitTests.Domain;

public class BuildArtifactTests
{
    private static BuildArtifact CreateArtifact(string? sha256 = null) => new(
        Guid.NewGuid(),
        "client.zip",
        "Windows Client",
        ArtifactType.GAME_CLIENT,
        "application/zip",
        1024,
        $"artifacts/{Guid.NewGuid():N}/client.zip",
        Guid.NewGuid(),
        "Tester",
        sha256);

    [Fact]
    public void Constructor_CreatesPendingArtifact()
    {
        var artifact = CreateArtifact();

        artifact.Status.Should().Be(ArtifactStatus.PENDING);
        artifact.SizeBytes.Should().Be(1024);
        artifact.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Constructor_NormalizesSha256()
    {
        var artifact = CreateArtifact(new string('A', 64));
        artifact.Sha256.Should().Be(new string('a', 64));
    }

    [Fact]
    public void Constructor_RejectsInvalidSha256()
    {
        var act = () => CreateArtifact("not-a-checksum");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_RejectsNonPositiveSize()
    {
        var act = () => new BuildArtifact(
            Guid.NewGuid(), "client.zip", "Client", ArtifactType.GAME_CLIENT, "application/zip", 0,
            "key", Guid.NewGuid(), "Tester");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Lifecycle_TransitionsUploadingVerifyingReady()
    {
        var artifact = CreateArtifact();
        artifact.MarkUploading();
        artifact.Status.Should().Be(ArtifactStatus.UPLOADING);

        artifact.MarkVerifying();
        artifact.Status.Should().Be(ArtifactStatus.VERIFYING);

        artifact.MarkReady();
        artifact.Status.Should().Be(ArtifactStatus.READY);
        artifact.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void ReadyArtifact_CannotReturnToUploading()
    {
        var artifact = CreateArtifact();
        artifact.MarkUploading();
        artifact.MarkVerifying();
        artifact.MarkReady();

        var act = artifact.MarkUploading;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void DeletedArtifact_CannotChangeState()
    {
        var artifact = CreateArtifact();
        artifact.MarkDeleted();

        artifact.IsDeleted.Should().BeTrue();
        artifact.Invoking(a => a.MarkUploading()).Should().Throw<InvalidOperationException>();
    }
}
