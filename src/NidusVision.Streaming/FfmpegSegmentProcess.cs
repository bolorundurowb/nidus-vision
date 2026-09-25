using System.Diagnostics;

namespace NidusVision.Streaming;

public sealed class FfmpegSegmentProcess
{
    public Process Start(string rtspUrl, string transport, string outputDirectory, int segmentDurationSeconds)
    {
        var startInfo = CreateStartInfo(rtspUrl, transport, outputDirectory, segmentDurationSeconds);
        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        FfmpegExecutable.Start(process);
        return process;
    }

    public ProcessStartInfo CreateStartInfo(string rtspUrl, string transport, string outputDirectory, int segmentDurationSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rtspUrl);
        ArgumentOutOfRangeException.ThrowIfLessThan(segmentDurationSeconds, 1);
        Directory.CreateDirectory(outputDirectory);
        var transportArg = transport.Equals("udp", StringComparison.OrdinalIgnoreCase) ? "udp" : "tcp";

        // The segment muxer expands strftime placeholders but never creates directories
        // (only the HLS muxer has strftime_mkdir), so segments stay flat in the camera
        // directory that was just created; a dated sub-path would fail with ENOENT.
        var output = Path.Combine(outputDirectory, "%Y%m%dT%H%M%S.mp4");
        var process = FfmpegExecutable.Create(startInfo =>
        {
            startInfo.ArgumentList.Add("-hide_banner");
            startInfo.ArgumentList.Add("-loglevel");
            startInfo.ArgumentList.Add("warning");
            startInfo.ArgumentList.Add("-rtsp_transport");
            startInfo.ArgumentList.Add(transportArg);
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(rtspUrl);
            startInfo.ArgumentList.Add("-an");
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("copy");
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("segment");
            startInfo.ArgumentList.Add("-segment_time");
            startInfo.ArgumentList.Add(Math.Min(
                segmentDurationSeconds,
                NidusVision.Core.Options.StorageOptions.MaxSegmentDurationSeconds).ToString());
            startInfo.ArgumentList.Add("-break_non_keyframes");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("-reset_timestamps");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("-strftime");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add(output);
        });
        return process.StartInfo;
    }
}
