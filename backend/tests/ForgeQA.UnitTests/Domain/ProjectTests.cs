using ForgeQA.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace ForgeQA.UnitTests.Domain;

public class ProjectTests
{
    [Fact]
    public void Constructor_Creates_Project_With_Expected_Values()
    {
        var organizationId = Guid.NewGuid();

        var project = new Project(organizationId, "FRONTLINE", "frontline", "Competitive FPS");

        project.OrganizationId.Should().Be(organizationId);
        project.Name.Should().Be("FRONTLINE");
        project.Slug.Should().Be("frontline");
        project.Description.Should().Be("Competitive FPS");
        project.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Constructor_Throws_When_OrganizationId_Is_Empty()
    {
        var act = () => new Project(Guid.Empty, "FRONTLINE", "frontline", null);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_Throws_When_Name_Is_Missing(string name)
    {
        var act = () => new Project(Guid.NewGuid(), name, "frontline", null);
        act.Should().Throw<ArgumentException>();
    }
}
