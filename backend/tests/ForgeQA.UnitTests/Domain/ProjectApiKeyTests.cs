using ForgeQA.Domain.Entities;
using ForgeQA.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ForgeQA.UnitTests.Domain;

public class ProjectApiKeyTests
{
    private static ProjectApiKey CreateKey(params ProjectApiKeyScope[] scopes) =>
        new(Guid.NewGuid(), "CI Runner", "fqa_proj_ab12cd34", "hashed-secret",
            scopes.Length == 0 ? new[] { ProjectApiKeyScope.BUG_REPORT_WRITE } : scopes, Guid.NewGuid());

    [Fact]
    public void Constructor_Creates_Active_Key()
    {
        var key = CreateKey();

        key.IsActive.Should().BeTrue();
        key.IsRevoked.Should().BeFalse();
        key.Scopes.Should().Contain(ProjectApiKeyScope.BUG_REPORT_WRITE);
    }

    [Fact]
    public void Constructor_Throws_When_No_Scopes_Given()
    {
        var act = () => new ProjectApiKey(Guid.NewGuid(), "Name", "prefix", "hash", Array.Empty<ProjectApiKeyScope>(), Guid.NewGuid());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void HasScope_Returns_False_When_Key_Is_Revoked()
    {
        var key = CreateKey(ProjectApiKeyScope.BUG_REPORT_WRITE);

        key.Revoke();

        key.HasScope(ProjectApiKeyScope.BUG_REPORT_WRITE).Should().BeFalse();
    }

    [Fact]
    public void Revoke_Is_Idempotent_And_Keeps_Original_Timestamp()
    {
        var key = CreateKey();

        key.Revoke();
        var firstRevokedAt = key.RevokedAt;
        key.Revoke();

        key.RevokedAt.Should().Be(firstRevokedAt);
    }

    [Fact]
    public void MarkUsed_Sets_LastUsedAt()
    {
        var key = CreateKey();

        key.MarkUsed();

        key.LastUsedAt.Should().NotBeNull();
    }
}
