using NidusVision.Streaming;

namespace NidusVision.Tests;

public sealed class FfmpegErrorLogTests
{
    [Fact]
    public void Describe_reports_authentication_failures()
    {
        const string log = """
            [rtsp @ 000001] method DESCRIBE failed: 401 Unauthorized
            rtsp://cam/live0: Server returned 401 Unauthorized
            """;
        Assert.Contains("username and password", FfmpegErrorLog.Describe(log, "fallback"));
    }

    [Fact]
    public void Describe_reports_unreachable_cameras()
    {
        Assert.Equal(
            "The camera could not be reached on the network.",
            FfmpegErrorLog.Describe("[tcp @ 1] Connection refused", "fallback"));
    }

    [Fact]
    public void Describe_redacts_credentials_from_unknown_errors()
    {
        var message = FfmpegErrorLog.Describe("Error opening rtsp://admin:s3cret@cam.local/live0 for reading", "fallback");
        Assert.DoesNotContain("s3cret", message);
        Assert.Contains("rtsp://***", message);
    }

    [Fact]
    public void Describe_falls_back_when_log_is_empty()
    {
        Assert.Equal("fallback", FfmpegErrorLog.Describe("   ", "fallback"));
    }

    [Fact]
    public void Appended_lines_are_capped_and_ordered()
    {
        var log = new FfmpegErrorLog();
        for (var i = 0; i < 60; i++)
        {
            log.Append($"line {i}");
        }

        var lines = log.Text.Split(Environment.NewLine);
        Assert.Equal(40, lines.Length);
        Assert.Equal("line 59", lines[^1]);
    }
}
