using System.Diagnostics;
using NidusVision.Core.Contracts;

namespace NidusVision.Streaming;

public sealed class RtspProbe
{
    public async Task<ProbeResult> ProbeAsync(string url, string transport, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        var transportArg = transport.Equals("udp", StringComparison.OrdinalIgnoreCase) ? "udp" : "tcp";
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                ArgumentList =
                {
                    "-hide_banner",
                    "-rtsp_transport",
                    transportArg,
                    "-i",
                    url,
                    "-t",
                    "2",
                    "-f",
                    "null",
                    "-",
                },
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return new ProbeResult(false, $"ffmpeg is not available: {ex.Message}", null, null);
        }

        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var (resolution, fps) = ParseStreamInfo(stderr);
        var ok = process.ExitCode is 0 || resolution is not null;
        return new ProbeResult(ok, ok ? "Stream reachable." : "Could not open RTSP stream.", resolution, fps);
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
