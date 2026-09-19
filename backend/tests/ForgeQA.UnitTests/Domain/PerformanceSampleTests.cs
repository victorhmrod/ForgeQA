using ForgeQA.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace ForgeQA.UnitTests.Domain;

public class PerformanceSampleTests
{
    private static PerformanceSample CreateSample(
        int sequenceNumber = 1,
        DateTime? clientTimestamp = null,
        double fps = 60.0,
        double frameTimeMs = 16.67,
        double? gameThreadTimeMs = null,
        double? cpuUtilizationPercent = null,
        long? memoryUsedBytes = null,
        int? drawCalls = null) =>
        new(Guid.NewGuid(), sequenceNumber, clientTimestamp ?? DateTime.UtcNow, fps, frameTimeMs,
            gameThreadTimeMs: gameThreadTimeMs, cpuUtilizationPercent: cpuUtilizationPercent,
            memoryUsedBytes: memoryUsedBytes, drawCalls: drawCalls);

    [Fact]
    public void Constructor_Creates_Sample_With_Required_Metrics()
    {
        var sample = CreateSample(fps: 92.4, frameTimeMs: 10.82);

        sample.Fps.Should().Be(92.4);
        sample.FrameTimeMs.Should().Be(10.82);
        sample.ReceivedAt.Should().NotBe(default);
        sample.GameThreadTimeMs.Should().BeNull();
    }

    [Fact]
    public void Constructor_Allows_Optional_Metrics_To_Be_Set()
    {
        var sample = CreateSample(gameThreadTimeMs: 4.1, cpuUtilizationPercent: 55.5, memoryUsedBytes: 8_589_934_592, drawCalls: 1200);

        sample.GameThreadTimeMs.Should().Be(4.1);
        sample.CpuUtilizationPercent.Should().Be(55.5);
        sample.MemoryUsedBytes.Should().Be(8_589_934_592);
        sample.DrawCalls.Should().Be(1200);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_Throws_When_SequenceNumber_Is_Not_Positive(int sequenceNumber)
    {
        var act = () => CreateSample(sequenceNumber: sequenceNumber);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Throws_When_ClientTimestamp_Is_Default()
    {
        var act = () => CreateSample(clientTimestamp: default(DateTime));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Throws_When_Fps_Is_Negative()
    {
        var act = () => CreateSample(fps: -1.0);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Throws_When_Fps_Is_NaN()
    {
        var act = () => CreateSample(fps: double.NaN);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Throws_When_FrameTimeMs_Is_Infinity()
    {
        var act = () => CreateSample(frameTimeMs: double.PositiveInfinity);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Throws_When_Optional_Timing_Is_Negative()
    {
        var act = () => CreateSample(gameThreadTimeMs: -0.1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Throws_When_Optional_Timing_Is_NaN()
    {
        var act = () => CreateSample(gameThreadTimeMs: double.NaN);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(100.1)]
    public void Constructor_Throws_When_Utilization_Percent_Is_Out_Of_Range(double percent)
    {
        var act = () => CreateSample(cpuUtilizationPercent: percent);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Allows_Utilization_Percent_At_Boundaries()
    {
        CreateSample(cpuUtilizationPercent: 0.0).CpuUtilizationPercent.Should().Be(0.0);
        CreateSample(cpuUtilizationPercent: 100.0).CpuUtilizationPercent.Should().Be(100.0);
    }

    [Fact]
    public void Constructor_Throws_When_MemoryUsedBytes_Is_Negative()
    {
        var act = () => CreateSample(memoryUsedBytes: -1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Throws_When_DrawCalls_Is_Negative()
    {
        var act = () => CreateSample(drawCalls: -1);

        act.Should().Throw<ArgumentException>();
    }
}
