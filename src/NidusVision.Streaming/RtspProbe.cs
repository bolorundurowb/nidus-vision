using NidusVision.Core.Contracts;

namespace NidusVision.Streaming;

public sealed class RtspProbe
{
    public async Task<ProbeResult> ProbeAsync(string url, string transport, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        var transportArg = transport.Equals("udp", StringComparison.OrdinalIgnoreCase) ? "udp" : "tcp";
        using var process = FfmpegExecutable.Create(startInfo =>
        {
            startInfo.ArgumentList.Add("-hide_banner");
            startInfo.ArgumentList.Add("-rtsp_transport");
            startInfo.ArgumentList.Add(transportArg);
            startInfo.ArgumentList.Add("-timeout");
            startInfo.ArgumentList.Add("10000000");
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(url);
            startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add("2");
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("null");
            startInfo.ArgumentList.Add("-");
        });

        try
        {
            FfmpegExecutable.Start(process);
        }
        catch (Exception ex)
        {
            return new ProbeResult(false, $"ffmpeg is not available: {ex.Message}", null, null);
        }

        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var (resolution, fps) = ParseStreamInfo(stderr);
        var ok = process.ExitCode is 0 || resolution is not null;
        var message = ok ? "Stream reachable." : FfmpegErrorLog.Describe(stderr, "Could not open the RTSP stream.");
        return new ProbeResult(ok, message, resolution, fps);
    }

    internal static (string? Resolution, int? Fps) ParseStreamInfo(string ffmpegLog)
    {
        string? resolution = null;
        int? fps = null;
        foreach (var line in ffmpegLog.AsSpan().ToString().Split('\n'))
        {
            var idx = line.IndexOf("Video:", StringComparison.Ordinal);
            if (idx < 0)
            {
                continue;
            }

            var dim = System.Text.RegularExpressions.Regex.Match(line, @"(\d{2,5})x(\d{2,5})");
            if (dim.Success)
            {
                resolution = $"{dim.Groups[1].Value}×{dim.Groups[2].Value}";
            }

            var fpsMatch = System.Text.RegularExpressions.Regex.Match(line, @"(\d+(?:\.\d+)?)\s*fps");
            if (fpsMatch.Success && int.TryParse(fpsMatch.Groups[1].Value.Split('.')[0], out var parsed))
            {
                fps = parsed;
            }
        }

        return (resolution, fps);
    }
}
