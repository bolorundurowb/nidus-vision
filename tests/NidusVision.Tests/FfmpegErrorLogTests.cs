using NidusVision.Streaming;

namespace NidusVision.Tests;

public sealed class FfmpegErrorLogTests
{
    [Fact]
    public void DescribeReportsAuthenticationFailures()
    {
        const string log = """
            [rtsp @ 000001] method DESCRIBE failed: 401 Unauthorized
            rtsp://cam/live0: Server returned 401 Unauthorized
            """;
        FfmpegErrorLog.Describe(log, "fallback").Must().Contain("username and password");
    }

    [Fact]
    public void DescribeReportsUnreachableCameras()
    {
        FfmpegErrorLog.Describe("[tcp @ 1] Connection refused", "fallback")
            .Must()
            .Be("The camera could not be reached on the network.");
    }

    [Fact]
    public void DescribeRedactsCredentialsFromUnknownErrors()
    {
        var message = FfmpegErrorLog.Describe("Error opening rtsp://admin:s3cret@cam.local/live0 for reading", "fallback");
        message.Must().NotContain("s3cret");
        message.Must().Contain("rtsp://***");
    }

    [Fact]
    public void DescribeFallsBackWhenLogIsEmpty()
    {
        FfmpegErrorLog.Describe("   ", "fallback").Must().Be("fallback");
    }

    [Fact]
    public void AppendedLinesAreCappedAndOrdered()
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
