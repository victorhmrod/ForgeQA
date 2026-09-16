using ForgeQA.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace ForgeQA.UnitTests.Domain;

public class TelemetryEventTests
{
    private static TelemetryEvent CreateEvent(
        int sequenceNumber = 1,
        string eventName = "weapon.fired",
        DateTime? clientTimestamp = null,
        string propertiesJson = "{}") =>
        new(Guid.NewGuid(), sequenceNumber, eventName, clientTimestamp ?? DateTime.UtcNow, null, propertiesJson, null);

    [Fact]
    public void Constructor_Creates_Event_With_ReceivedAt_Set()
    {
        var evt = CreateEvent();

        evt.ReceivedAt.Should().NotBe(default);
        evt.PropertiesJson.Should().Be("{}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_Throws_When_SequenceNumber_Is_Not_Positive(int sequenceNumber)
    {
        var act = () => CreateEvent(sequenceNumber: sequenceNumber);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Throws_When_ClientTimestamp_Is_Default()
    {
        var act = () => CreateEvent(clientTimestamp: default(DateTime));

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("has spaces")]
    [InlineData("has/slash")]
    [InlineData("semi;colon")]
    public void Constructor_Throws_When_EventName_Is_Invalid(string eventName)
    {
        var act = () => CreateEvent(eventName: eventName);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("weapon.fired")]
    [InlineData("player_died")]
    [InlineData("ui-menu-opened")]
    [InlineData("qa.checkpoint-1")]
    public void Constructor_Accepts_Valid_EventNames(string eventName)
    {
        var evt = CreateEvent(eventName: eventName);

        evt.EventName.Should().Be(eventName);
    }

    [Fact]
    public void Constructor_Throws_When_EventName_Exceeds_Max_Length()
    {
        var act = () => CreateEvent(eventName: new string('a', TelemetryEvent.EventNameMaxLength + 1));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Defaults_Empty_PropertiesJson_To_Empty_Object()
    {
        var evt = CreateEvent(propertiesJson: "   ");

        evt.PropertiesJson.Should().Be("{}");
    }
}
