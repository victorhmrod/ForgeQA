using ForgeQA.Domain.Entities;
using ForgeQA.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ForgeQA.UnitTests.Domain;

public class BugAttachmentTests
{
    private static BugAttachment CreateAttachment() =>
        new(Guid.NewGuid(), BugAttachmentType.SCREENSHOT, "bug.png", "image/png", 1024, "orgs/1/projects/2/bugs/3/attachments/4/bug.png");

    [Fact]
    public void Constructor_Creates_Attachment_In_Pending_Status()
    {
        var attachment = CreateAttachment();

        attachment.Status.Should().Be(BugAttachmentStatus.PENDING);
        attachment.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Constructor_Throws_When_SizeBytes_Is_Not_Positive()
    {
        var act = () => new BugAttachment(Guid.NewGuid(), BugAttachmentType.SCREENSHOT, "bug.png", "image/png", 0, "key");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkReady_Transitions_From_Pending()
    {
        var attachment = CreateAttachment();

        attachment.MarkReady();

        attachment.Status.Should().Be(BugAttachmentStatus.READY);
    }

    [Fact]
    public void MarkFailed_Then_MarkReady_Throws()
    {
        var attachment = CreateAttachment();
        attachment.MarkFailed();

        var act = () => attachment.MarkReady();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SoftDelete_Is_Idempotent()
    {
        var attachment = CreateAttachment();

        attachment.SoftDelete();
        var firstDeletedAt = attachment.DeletedAt;
        attachment.SoftDelete();

        attachment.DeletedAt.Should().Be(firstDeletedAt);
    }
}
