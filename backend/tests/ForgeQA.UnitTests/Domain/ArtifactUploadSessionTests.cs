using FluentAssertions;
using ForgeQA.Domain.Entities;

namespace ForgeQA.UnitTests.Domain;

public class ArtifactUploadSessionTests
{
    [Fact]
    public void Constructor_CreatesActiveSession()
    {
        var session = new ArtifactUploadSession(Guid.NewGuid(), "upload-123", 64 * 1024 * 1024, 4, DateTime.UtcNow.AddHours(1));

        session.IsCompleted.Should().BeFalse();
        session.IsAborted.Should().BeFalse();
        session.IsExpired.Should().BeFalse();
        session.ExpectedParts.Should().Be(4);
    }

    [Fact]
    public void CompletedSession_CannotBeAborted()
    {
        var session = new ArtifactUploadSession(Guid.NewGuid(), "upload-123", 1024, 1, DateTime.UtcNow.AddHours(1));
        session.MarkCompleted();

        session.Invoking(value => value.MarkAborted()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AbortedSession_CannotBeCompleted()
    {
        var session = new ArtifactUploadSession(Guid.NewGuid(), "upload-123", 1024, 1, DateTime.UtcNow.AddHours(1));
        session.MarkAborted();

        session.Invoking(value => value.MarkCompleted()).Should().Throw<InvalidOperationException>();
    }
}
