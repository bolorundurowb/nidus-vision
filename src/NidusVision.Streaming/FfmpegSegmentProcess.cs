using System.Diagnostics;

namespace NidusVision.Streaming;

public sealed class FfmpegSegmentProcess
{
    public Process Start(string rtspUrl, string transport, string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rtspUrl);
        Directory.CreateDirectory(outputDirectory);
        var transportArg = transport.Equals("udp", StringComparison.OrdinalIgnoreCase) ? "udp" : "tcp";
        var output = Path.Combine(outputDirectory, "%Y%m%dT%H%M%S.mp4");
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                ArgumentList =
                {
                    "-hide_banner",
                    "-loglevel",
                    "warning",
                    "-rtsp_transport",
                    transportArg,
                    "-i",
                    rtspUrl,
                    "-an",
                    "-c",
                    "copy",
                    "-f",
                    "segment",
                    "-segment_time",
                    "10",
                    "-reset_timestamps",
                    "1",
                    "-strftime",
                    "1",
                    output,
                },
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };
        process.Start();
        return process;
    }
}
