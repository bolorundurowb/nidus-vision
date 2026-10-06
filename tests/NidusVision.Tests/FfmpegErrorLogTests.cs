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

    [Theory]
    [InlineData("Error opening rtsps://admin:s3cret@cam.local:322/live0 for reading", "rtsps://***@cam.local:322/live0")]
    [InlineData("[rtsp @ 0x1] rtsp://admin:p@ss:s3cret@10.0.0.5/h264: Connection reset", "rtsp://***@10.0.0.5/h264")]
    [InlineData("Could not open 'rtsp://user:s3cret@cam/live'", "'rtsp://***@cam/live'")]
    [InlineData("rtsp://cam/cgi?user=admin&password=s3cret&channel=1: 404", "password=***&channel=1")]
    public void RedactMasksCredentialsButKeepsTheHost(string line, string expected)
    {
        // Act
        var redacted = FfmpegErrorLog.Redact(line);

        // Assert
        redacted.Must().NotContain("s3cret");
        redacted.Must().Contain(expected);
    }

    [Fact]
    public void RedactLeavesLinesWithoutCredentialsAlone()
    {
        // Arrange
        const string line = "rtsp://cam.local/live0: Server returned 404 Not Found";

        // Act & Assert
        FfmpegErrorLog.Redact(line).Must().Be(line);
    }

    [Fact]
    public void AppendedLinesAreRedactedBeforeTheyReachText()
    {
        // Arrange
        var log = new FfmpegErrorLog();

        // Act
        log.Append("[in#0 @ 0x2] Error opening input: rtsp://admin:s3cret@cam.local/live0: Connection refused");

        // Assert
        log.Text.Must().NotContain("s3cret");
        log.Text.Must().Contain("rtsp://***@cam.local/live0");
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
