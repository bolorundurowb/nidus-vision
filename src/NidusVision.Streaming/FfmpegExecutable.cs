using System.Diagnostics;
using System.Text;

namespace NidusVision.Streaming;

public static class FfmpegExecutable
{
    public static string FileName =>
        Environment.GetEnvironmentVariable("FFMPEG_PATH") is { Length: > 0 } path
            ? path
            : "ffmpeg";

    public static bool IsAvailable() =>
        !Path.IsPathRooted(FileName) || File.Exists(FileName);

    public static Process Create(Action<ProcessStartInfo> configure)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = FileName,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        configure(startInfo);
        return new Process { StartInfo = startInfo, EnableRaisingEvents = true };
    }

    public static void Start(Process process)
    {
        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException(
                $"FFmpeg was not found ('{FileName}'). The Docker image installs it at /usr/bin/ffmpeg (FFMPEG_PATH). For local `dotnet run`, install FFmpeg or set FFMPEG_PATH.",
                ex);
        }
    }

    /// <summary>
    /// Drains stderr continuously; FFmpeg blocks once the redirected pipe fills, which stalls the stream.
    /// </summary>
    public static FfmpegErrorLog CaptureErrors(Process process)
    {
        var log = new FfmpegErrorLog();
        process.ErrorDataReceived += (_, args) => log.Append(args.Data);
        process.BeginErrorReadLine();
        return log;
    }
}

public sealed class FfmpegErrorLog
{
    private const int MaxLines = 40;
    private readonly Queue<string> _lines = new();
    private readonly Lock _gate = new();

    public void Append(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        lock (_gate)
        {
            _lines.Enqueue(line.Trim());
            if (_lines.Count > MaxLines)
            {
                _lines.Dequeue();
            }
        }
    }

    public string Text
    {
        get
        {
            lock (_gate)
            {
                return string.Join(Environment.NewLine, _lines);
            }
        }
    }

    public string Describe(string fallback) => Describe(Text, fallback);

    /// <summary>Condenses an FFmpeg log into a single line that is useful in the UI.</summary>
    public static string Describe(string log, string fallback)
    {
        var lines = log.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            if (line.Contains("401", StringComparison.Ordinal) || line.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
            {
                return "The camera rejected the credentials (401 Unauthorized). Add the camera username and password.";
            }

            if (line.Contains("Connection refused", StringComparison.OrdinalIgnoreCase)
                || line.Contains("No route to host", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Network is unreachable", StringComparison.OrdinalIgnoreCase))
            {
                return "The camera could not be reached on the network.";
            }

            if (line.Contains("timed out", StringComparison.OrdinalIgnoreCase) || line.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            {
                return "The camera did not respond before the timeout expired.";
            }

            if (line.Contains("404", StringComparison.Ordinal) || line.Contains("Not Found", StringComparison.OrdinalIgnoreCase))
            {
                return "The RTSP path was not found on the camera (404).";
            }
        }

        var last = Array.FindLast(lines, l => l.Contains("rror", StringComparison.Ordinal))
            ?? lines.LastOrDefault();
        return string.IsNullOrWhiteSpace(last) ? fallback : Sanitize(last);
    }

    /// <summary>Strips RTSP userinfo so FFmpeg output never leaks camera credentials.</summary>
    private static string Sanitize(string line)
    {
        var builder = new StringBuilder(line.Length);
        var index = 0;
        while (index < line.Length)
        {
            var at = line.IndexOf("rtsp://", index, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                builder.Append(line, index, line.Length - index);
                break;
            }

            var end = line.IndexOfAny([' ', '\'', '"'], at);
            end = end < 0 ? line.Length : end;
            builder.Append(line, index, at - index).Append("rtsp://***");
            index = end;
        }

        return builder.ToString();
    }
}
