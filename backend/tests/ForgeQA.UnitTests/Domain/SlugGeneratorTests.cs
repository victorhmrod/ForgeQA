using ForgeQA.Domain.Common;
using FluentAssertions;
using Xunit;

namespace ForgeQA.UnitTests.Domain;

public class SlugGeneratorTests
{
    [Theory]
    [InlineData("FRONTLINE", "frontline")]
    [InlineData("Frontline FPS", "frontline-fps")]
    [InlineData("  Victor's Workspace  ", "victor-s-workspace")]
    [InlineData("Build v1.2.3!!", "build-v1-2-3")]
    public void Generate_Produces_Url_Safe_Lowercase_Slug(string input, string expected)
    {
        SlugGenerator.Generate(input).Should().Be(expected);
    }

    [Fact]
    public void Generate_Throws_When_Input_Is_Empty()
    {
        var act = () => SlugGenerator.Generate("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Generate_Returns_Fallback_When_No_Alphanumeric_Characters()
    {
        SlugGenerator.Generate("!!!").Should().Be("n-a");
    }
}
