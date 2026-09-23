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
        FfmpegErrorLog.Describe(log, "fallback").Must().Contain("username and password");
    }

    [Fact]
    public void Describe_reports_unreachable_cameras()
    {
        FfmpegErrorLog.Describe("[tcp @ 1] Connection refused", "fallback")
            .Must()
            .Be("The camera could not be reached on the network.");
    }

    [Fact]
    public void Describe_redacts_credentials_from_unknown_errors()
    {
        var message = FfmpegErrorLog.Describe("Error opening rtsp://admin:s3cret@cam.local/live0 for reading", "fallback");
        message.Must().NotContain("s3cret");
        message.Must().Contain("rtsp://***");
    }

    [Fact]
    public void Describe_falls_back_when_log_is_empty()
    {
        FfmpegErrorLog.Describe("   ", "fallback").Must().Be("fallback");
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
        lines.Length.Must().Be(40);
        lines[^1].Must().Be("line 59");
    }
}
