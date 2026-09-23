using NidusVision.Core.Cameras;

namespace NidusVision.Tests;

public sealed class CameraStreamStatsTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-23T12:00:00Z");

    [Fact]
    public void Bitrate_averages_the_sampled_segments()
    {
        var samples = new[]
        {
            new SegmentStatSample(Now.AddMinutes(-60), Now.AddMinutes(-30), 720_000_000),
            new SegmentStatSample(Now.AddMinutes(-30), Now, 720_000_000),
        };

        CameraStreamStats.Bitrate(samples, Now).Must().Be("3.2 Mbps");
    }

    [Fact]
    public void Bitrate_stops_an_in_progress_segment_at_the_current_time()
    {
        var samples = new[] { new SegmentStatSample(Now.AddMinutes(-10), Now.AddMinutes(20), 75_000_000) };

        CameraStreamStats.Bitrate(samples, Now).Must().Be("1 Mbps");
    }

    [Fact]
    public void Bitrate_falls_back_to_kbps_for_low_rate_streams()
    {
        var samples = new[] { new SegmentStatSample(Now.AddMinutes(-10), Now, 60_000_000) };

        CameraStreamStats.Bitrate(samples, Now).Must().Be("800 kbps");
    }

    [Fact]
    public void Bitrate_is_unknown_until_the_sample_is_long_enough()
    {
        var samples = new[] { new SegmentStatSample(Now.AddSeconds(-5), Now, 75_000_000) };

        CameraStreamStats.Bitrate(samples, Now).VerifyNullable().BeNull();
    }

    [Fact]
    public void Bitrate_is_unknown_without_recorded_bytes()
    {
        var samples = new[] { new SegmentStatSample(Now.AddMinutes(-30), Now, 0) };

        CameraStreamStats.Bitrate(samples, Now).VerifyNullable().BeNull();
    }

    [Theory]
    [InlineData(-3, 0, "3 days")]
    [InlineData(-1, 0, "1 day")]
    [InlineData(0, -5, "5 hours")]
    [InlineData(0, -1, "1 hour")]
    public void Retention_reports_how_far_back_footage_reaches(int days, int hours, string expected)
    {
        var oldest = Now.AddDays(days).AddHours(hours);

        CameraStreamStats.Retention(oldest, Now).Must().Be(expected);
    }

    [Fact]
    public void Retention_reports_sub_hour_footage_in_minutes()
    {
        CameraStreamStats.Retention(Now.AddMinutes(-12), Now).Must().Be("12 minutes");
        CameraStreamStats.Retention(Now.AddSeconds(-20), Now).Must().Be("< 1 minute");
    }

    [Fact]
    public void Retention_is_unknown_without_recordings()
    {
        CameraStreamStats.Retention(null, Now).VerifyNullable().BeNull();
    }
}
