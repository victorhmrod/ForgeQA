using ForgeQA.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace ForgeQA.UnitTests.Domain;

public class TelemetrySessionTests
{
    private static TelemetrySession CreateSession() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null);

    [Fact]
    public void Constructor_Creates_Active_Session()
    {
        var session = CreateSession();

        session.IsActive.Should().BeTrue();
        session.EndedAt.Should().BeNull();
        session.LastEventAt.Should().BeNull();
        session.Environment.Should().NotBeNull();
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Constructor_Throws_When_Required_Identity_Is_Missing(bool emptyProject, bool emptyBuild, bool emptyRuntimeSession)
    {
        var act = () => new TelemetrySession(
            emptyProject ? Guid.Empty : Guid.NewGuid(),
            emptyBuild ? Guid.Empty : Guid.NewGuid(),
            emptyRuntimeSession ? Guid.Empty : Guid.NewGuid(),
            null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RecordEventsReceived_Updates_LastEventAt()
    {
        var session = CreateSession();

        session.RecordEventsReceived();

        session.LastEventAt.Should().NotBeNull();
    }

    [Fact]
    public void End_Sets_EndedAt_And_Is_Idempotent()
    {
        var session = CreateSession();

        session.End();
        var firstEndedAt = session.EndedAt;
        session.End();

        session.IsActive.Should().BeFalse();
        session.EndedAt.Should().Be(firstEndedAt);
    }
}
