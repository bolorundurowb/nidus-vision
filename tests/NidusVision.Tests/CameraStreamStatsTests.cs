using NidusVision.Core.Cameras;

namespace NidusVision.Tests;

public sealed class CameraStreamStatsTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-23T12:00:00Z");

    [Fact]
    public void BitrateAveragesTheSampledSegments()
    {
        var samples = new[]
        {
            new SegmentStatSample(Now.AddMinutes(-60), Now.AddMinutes(-30), 720_000_000),
            new SegmentStatSample(Now.AddMinutes(-30), Now, 720_000_000),
        };

        CameraStreamStats.Bitrate(samples, Now).Must().Be("3.2 Mbps");
    }

    [Fact]
    public void BitrateStopsAnInProgressSegmentAtTheCurrentTime()
    {
        var samples = new[] { new SegmentStatSample(Now.AddMinutes(-10), Now.AddMinutes(20), 75_000_000) };

        CameraStreamStats.Bitrate(samples, Now).Must().Be("1 Mbps");
    }

    [Fact]
    public void BitrateFallsBackToKbpsForLowRateStreams()
    {
        var samples = new[] { new SegmentStatSample(Now.AddMinutes(-10), Now, 60_000_000) };

        CameraStreamStats.Bitrate(samples, Now).Must().Be("800 kbps");
    }

    [Fact]
    public void BitrateIsUnknownUntilTheSampleIsLongEnough()
    {
        var samples = new[] { new SegmentStatSample(Now.AddSeconds(-5), Now, 75_000_000) };

        CameraStreamStats.Bitrate(samples, Now).VerifyNullable().BeNull();
    }

    [Fact]
    public void BitrateIsUnknownWithoutRecordedBytes()
    {
        var samples = new[] { new SegmentStatSample(Now.AddMinutes(-30), Now, 0) };

        CameraStreamStats.Bitrate(samples, Now).VerifyNullable().BeNull();
    }

    [Theory]
    [InlineData(-3, 0, "3 days")]
    [InlineData(-1, 0, "1 day")]
    [InlineData(0, -5, "5 hours")]
    [InlineData(0, -1, "1 hour")]
    public void RetentionReportsHowFarBackFootageReaches(int days, int hours, string expected)
    {
        // Arrange
        var oldest = Now.AddDays(days).AddHours(hours);

        // Act
        var retention = CameraStreamStats.Retention(oldest, Now);

        // Assert
        retention.Must().Be(expected);
    }

    [Theory]
    [InlineData(-720, "12 minutes")]
    [InlineData(-20, "< 1 minute")]
    public void RetentionWithSubHourFootageReturnsMinuteDescription(int seconds, string expected)
    {
        var retention = CameraStreamStats.Retention(Now.AddSeconds(seconds), Now);

        retention.Must().Be(expected);
    }

    [Fact]
    public void RetentionIsUnknownWithoutRecordings()
    {
        CameraStreamStats.Retention(null, Now).VerifyNullable().BeNull();
    }
}
