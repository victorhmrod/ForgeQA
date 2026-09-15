using ForgeQA.Domain.Entities;
using ForgeQA.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ForgeQA.UnitTests.Domain;

public class BuildTests
{
    private static Build CreateBuild(
        string version = "0.4.2",
        string buildNumber = "1842",
        BuildPlatform platform = BuildPlatform.WINDOWS,
        BuildConfiguration configuration = BuildConfiguration.DEVELOPMENT) =>
        new(Guid.NewGuid(), version, buildNumber, platform, configuration, Guid.NewGuid(), "Victor");

    [Fact]
    public void Constructor_Creates_Build_With_Expected_Values()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var build = new Build(
            projectId, "0.4.2", "1842", BuildPlatform.WINDOWS, BuildConfiguration.DEVELOPMENT,
            userId, "Victor", name: "QA Candidate", branch: "main", commitSha: "a941de3",
            engineVersion: "UE 5.8.2", changelog: "Fixes.");

        build.ProjectId.Should().Be(projectId);
        build.Version.Should().Be("0.4.2");
        build.BuildNumber.Should().Be("1842");
        build.Platform.Should().Be(BuildPlatform.WINDOWS);
        build.Configuration.Should().Be(BuildConfiguration.DEVELOPMENT);
        build.Name.Should().Be("QA Candidate");
        build.Branch.Should().Be("main");
        build.CommitSha.Should().Be("a941de3");
        build.EngineVersion.Should().Be("UE 5.8.2");
        build.Changelog.Should().Be("Fixes.");
        build.CreatedByUserId.Should().Be(userId);
        build.CreatedByDisplayName.Should().Be("Victor");
        build.IsArchived.Should().BeFalse();
        build.ArchivedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_Throws_When_Version_Is_Missing(string version)
    {
        var act = () => CreateBuild(version: version);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_Throws_When_BuildNumber_Is_Missing(string buildNumber)
    {
        var act = () => CreateBuild(buildNumber: buildNumber);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Throws_When_Version_Exceeds_Max_Length()
    {
        var act = () => CreateBuild(version: new string('a', Build.VersionMaxLength + 1));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Trims_Optional_Empty_Strings_To_Null()
    {
        var build = CreateBuild();
        build.Name.Should().BeNull();
        build.Branch.Should().BeNull();
        build.CommitSha.Should().BeNull();
        build.EngineVersion.Should().BeNull();
        build.Changelog.Should().BeNull();
    }

    [Fact]
    public void Archive_Sets_ArchivedAt_And_IsArchived()
    {
        var build = CreateBuild();

        build.Archive();

        build.IsArchived.Should().BeTrue();
        build.ArchivedAt.Should().NotBeNull();
    }

    [Fact]
    public void Archive_Is_Idempotent()
    {
        var build = CreateBuild();
        build.Archive();
        var firstArchivedAt = build.ArchivedAt;

        build.Archive();

        build.ArchivedAt.Should().Be(firstArchivedAt);
    }

    [Fact]
    public void Restore_Clears_ArchivedAt()
    {
        var build = CreateBuild();
        build.Archive();

        build.Restore();

        build.IsArchived.Should().BeFalse();
        build.ArchivedAt.Should().BeNull();
    }

    [Fact]
    public void UpdateMetadata_Changes_Editable_Fields_Only()
    {
        var build = CreateBuild();

        build.UpdateMetadata("0.5.0", "Renamed", "release", "b123456", "UE 5.8.3", "New notes");

        build.Version.Should().Be("0.5.0");
        build.Name.Should().Be("Renamed");
        build.Branch.Should().Be("release");
        build.CommitSha.Should().Be("b123456");
        build.EngineVersion.Should().Be("UE 5.8.3");
        build.Changelog.Should().Be("New notes");

        // Identity fields are not exposed by UpdateMetadata and remain unchanged.
        build.BuildNumber.Should().Be("1842");
        build.Platform.Should().Be(BuildPlatform.WINDOWS);
        build.Configuration.Should().Be(BuildConfiguration.DEVELOPMENT);
    }
}
