using ForgeQA.Domain.Entities;
using ForgeQA.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ForgeQA.UnitTests.Domain;

public class BugReportTests
{
    private static BugReport CreateWebBug(
        Guid? buildId = null,
        BugSeverity severity = BugSeverity.MEDIUM) =>
        new(Guid.NewGuid(), "Weapon stays ADS after reload", severity, BugSource.WEB, buildId,
            "Description", "1. ADS\n2. Reload", Guid.NewGuid(), null, null, null);

    [Fact]
    public void Constructor_Creates_Bug_With_Open_Status()
    {
        var bug = CreateWebBug();

        bug.Status.Should().Be(BugStatus.OPEN);
        bug.ResolvedAt.Should().BeNull();
        bug.ClosedAt.Should().BeNull();
        bug.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Constructor_Throws_When_UnrealRuntime_Source_Has_No_BuildId()
    {
        var act = () => new BugReport(
            Guid.NewGuid(), "Title", BugSeverity.HIGH, BugSource.UNREAL_RUNTIME, null,
            null, null, null, null, null, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Allows_UnrealRuntime_Source_With_BuildId()
    {
        var buildId = Guid.NewGuid();
        var bug = new BugReport(
            Guid.NewGuid(), "Title", BugSeverity.HIGH, BugSource.UNREAL_RUNTIME, buildId,
            null, null, null, null, Guid.NewGuid(), null);

        bug.BuildId.Should().Be(buildId);
        bug.Source.Should().Be(BugSource.UNREAL_RUNTIME);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_Throws_When_Title_Is_Missing(string title)
    {
        var act = () => new BugReport(
            Guid.NewGuid(), title, BugSeverity.LOW, BugSource.WEB, null, null, null, null, null, null, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Throws_When_Title_Exceeds_Max_Length()
    {
        var act = () => new BugReport(
            Guid.NewGuid(), new string('a', BugReport.TitleMaxLength + 1), BugSeverity.LOW, BugSource.WEB,
            null, null, null, null, null, null, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_Changes_Human_Authored_Fields_Only()
    {
        var bug = CreateWebBug();
        var originalCreatedAt = bug.CreatedAt;
        var originalSource = bug.Source;

        bug.UpdateDetails("New title", "New description", "New steps", BugSeverity.CRITICAL);

        bug.Title.Should().Be("New title");
        bug.Description.Should().Be("New description");
        bug.ReproductionSteps.Should().Be("New steps");
        bug.Severity.Should().Be(BugSeverity.CRITICAL);
        bug.Source.Should().Be(originalSource);
        bug.CreatedAt.Should().Be(originalCreatedAt);
    }

    [Fact]
    public void ChangeStatus_To_Resolved_Sets_ResolvedAt()
    {
        var bug = CreateWebBug();

        bug.ChangeStatus(BugStatus.RESOLVED);

        bug.Status.Should().Be(BugStatus.RESOLVED);
        bug.ResolvedAt.Should().NotBeNull();
        bug.ClosedAt.Should().BeNull();
    }

    [Fact]
    public void ChangeStatus_To_Closed_Sets_ClosedAt_And_ResolvedAt()
    {
        var bug = CreateWebBug();

        bug.ChangeStatus(BugStatus.CLOSED);

        bug.Status.Should().Be(BugStatus.CLOSED);
        bug.ClosedAt.Should().NotBeNull();
        bug.ResolvedAt.Should().NotBeNull("closing directly from OPEN implies it was also resolved");
    }

    [Fact]
    public void ChangeStatus_Reopening_Clears_Terminal_Timestamps()
    {
        var bug = CreateWebBug();
        bug.ChangeStatus(BugStatus.CLOSED);

        bug.ChangeStatus(BugStatus.OPEN);

        bug.Status.Should().Be(BugStatus.OPEN);
        bug.ResolvedAt.Should().BeNull();
        bug.ClosedAt.Should().BeNull();
    }

    [Fact]
    public void ChangeStatus_To_Same_Status_Is_A_NoOp()
    {
        var bug = CreateWebBug();
        bug.ChangeStatus(BugStatus.RESOLVED);
        var resolvedAt = bug.ResolvedAt;

        bug.ChangeStatus(BugStatus.RESOLVED);

        bug.ResolvedAt.Should().Be(resolvedAt);
    }

    [Fact]
    public void SoftDelete_Sets_DeletedAt_And_Is_Idempotent()
    {
        var bug = CreateWebBug();

        bug.SoftDelete();
        var firstDeletedAt = bug.DeletedAt;
        bug.SoftDelete();

        bug.IsDeleted.Should().BeTrue();
        bug.DeletedAt.Should().Be(firstDeletedAt);
    }
}
