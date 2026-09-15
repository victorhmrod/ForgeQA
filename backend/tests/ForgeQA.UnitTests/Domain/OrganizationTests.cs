using ForgeQA.Domain.Entities;
using ForgeQA.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ForgeQA.UnitTests.Domain;

public class OrganizationTests
{
    [Fact]
    public void AddMember_Adds_Member_With_Given_Role()
    {
        var organization = new Organization("Victor's Workspace", "victors-workspace");
        var userId = Guid.NewGuid();

        var member = organization.AddMember(userId, OrganizationRole.Owner);

        member.UserId.Should().Be(userId);
        member.Role.Should().Be(OrganizationRole.Owner);
        organization.Members.Should().ContainSingle(m => m.UserId == userId);
    }

    [Fact]
    public void AddMember_Throws_When_User_Is_Already_A_Member()
    {
        var organization = new Organization("Victor's Workspace", "victors-workspace");
        var userId = Guid.NewGuid();
        organization.AddMember(userId, OrganizationRole.Owner);

        var act = () => organization.AddMember(userId, OrganizationRole.Member);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("", "slug")]
    [InlineData("Name", "")]
    public void Constructor_Throws_When_Name_Or_Slug_Missing(string name, string slug)
    {
        var act = () => new Organization(name, slug);
        act.Should().Throw<ArgumentException>();
    }
}
