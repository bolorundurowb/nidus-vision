using System.Diagnostics;
using System.Globalization;

namespace NidusVision.Streaming;

public sealed class FfmpegSegmentProcess
{
    public const int DetectionInputSize = 640;
    public static int DetectionFrameBytes => DetectionInputSize * DetectionInputSize * 3;

    public Process Start(
        string rtspUrl,
        string transport,
        string outputDirectory,
        int segmentDurationSeconds,
        float? detectionSampleFps = null)
    {
        var startInfo = CreateStartInfo(rtspUrl, transport, outputDirectory, segmentDurationSeconds, detectionSampleFps);
        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        FfmpegExecutable.Start(process);
        return process;
    }

    public ProcessStartInfo CreateStartInfo(
        string rtspUrl,
        string transport,
        string outputDirectory,
        int segmentDurationSeconds,
        float? detectionSampleFps = null)
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
            startInfo.Environment["TZ"] = "UTC";
            startInfo.ArgumentList.Add("-hide_banner");
            startInfo.ArgumentList.Add("-loglevel");
            startInfo.ArgumentList.Add("warning");
            startInfo.ArgumentList.Add("-rtsp_transport");
            startInfo.ArgumentList.Add(transportArg);
            startInfo.ArgumentList.Add("-timeout");
            startInfo.ArgumentList.Add("10000000");
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
                NidusVision.Core.Options.StorageOptions.MaxSegmentDurationSeconds).ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("-reset_timestamps");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("-strftime");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add(output);

            if (detectionSampleFps is > 0)
            {
                var fps = Math.Clamp(detectionSampleFps.Value, 0.2f, 5f).ToString("0.###", CultureInfo.InvariantCulture);
                startInfo.ArgumentList.Add("-an");
                startInfo.ArgumentList.Add("-vf");
                startInfo.ArgumentList.Add(
                    $"fps={fps},scale={DetectionInputSize}:{DetectionInputSize}:force_original_aspect_ratio=decrease,pad={DetectionInputSize}:{DetectionInputSize}:(ow-iw)/2:(oh-ih)/2:color=0x727272");
                startInfo.ArgumentList.Add("-f");
                startInfo.ArgumentList.Add("rawvideo");
                startInfo.ArgumentList.Add("-pix_fmt");
                startInfo.ArgumentList.Add("rgb24");
                startInfo.ArgumentList.Add("pipe:1");
            }
        });
        return process.StartInfo;
    }
}
