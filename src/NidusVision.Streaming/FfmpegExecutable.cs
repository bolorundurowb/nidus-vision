using System.Diagnostics;
using System.Text.RegularExpressions;

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
            FfmpegProcessGroup.Track(process);
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

public sealed partial class FfmpegErrorLog
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
            // Redact on the way in so Text, Describe, and anything that logs them never hold
            // camera credentials. FFmpeg echoes the input URL, userinfo included, on most errors.
            _lines.Enqueue(Redact(line.Trim()));
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

    /// <summary>
    /// Masks credentials in FFmpeg output: the userinfo of any URL (<c>rtsp://user:pass@host</c>,
    /// also <c>rtsps</c>, <c>http(s)</c>, and <c>rtmp(s)</c>) and <c>password=</c>-style query
    /// parameters that some cameras take instead. The host and path stay visible for diagnostics.
    /// </summary>
    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        var masked = UrlUserInfo().Replace(text, "$1***@");
        return SecretQueryParameter().Replace(masked, "$1***");
    }

    private static string Sanitize(string line) => Redact(line);

    // Greedy up to the last '@' before the path, so a raw '@' inside the password is covered too.
    [GeneratedRegex(@"(\b(?:rtsps?|rtmps?|https?)://)[^\s/'""]+@", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlUserInfo();

    [GeneratedRegex(@"(\b(?:password|passwd|pwd|pass)=)[^&\s'""]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretQueryParameter();
}
