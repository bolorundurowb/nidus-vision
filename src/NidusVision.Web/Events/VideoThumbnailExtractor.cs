using System.Diagnostics;
using System.Globalization;
using NidusVision.Core.Storage;
using NidusVision.Streaming;

namespace NidusVision.Web.Events;

public sealed class VideoThumbnailExtractor
{
    public static readonly TimeSpan RecordingSeek = TimeSpan.FromSeconds(2);
    public const int Width = 640;
    public const int BatchSize = 8;

    public static bool NeedsThumbnail(string? storedPath) =>
        string.IsNullOrWhiteSpace(storedPath) || !File.Exists(storedPath);

    public static bool IsSourceReady(string path)
    {
        var info = new FileInfo(path);
        // Recording segments are fragmented MP4s, so an open file can be read as
        // soon as FFmpeg has emitted its initial metadata and first fragment.
        // Failed early attempts remain unindexed and are retried next cycle.
        return info.Exists && info.Length > 0;
    }

    public static string DestinationForRecording(string recordingPath) =>
        RecordingPath.ThumbnailFor(recordingPath);

    public static void ConfigureExtract(ProcessStartInfo startInfo, string sourcePath, string destination, TimeSpan seek)
    {
        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-loglevel");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-ss");
        startInfo.ArgumentList.Add(FormatTimestamp(seek));
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(sourcePath);
        startInfo.ArgumentList.Add("-frames:v");
        startInfo.ArgumentList.Add("1");
        startInfo.ArgumentList.Add("-vf");
        startInfo.ArgumentList.Add($"scale={Width}:-2");
        startInfo.ArgumentList.Add("-q:v");
        startInfo.ArgumentList.Add("4");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("mjpeg");
        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add(destination);
    }

    public async Task<string?> ExtractAsync(string sourcePath, string destination, TimeSpan seek, CancellationToken cancellationToken)
    {
        if (!FfmpegExecutable.IsAvailable() || !File.Exists(sourcePath))
        {
            return null;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (await TryExtractAsync(sourcePath, destination, seek, cancellationToken)
            || (seek > TimeSpan.Zero && await TryExtractAsync(sourcePath, destination, TimeSpan.Zero, cancellationToken)))
        {
            return destination;
        }

        return null;
    }

    private static async Task<bool> TryExtractAsync(
        string sourcePath,
        string destination,
        TimeSpan seek,
        CancellationToken cancellationToken)
    {
        var temporary = $"{destination}.{Guid.CreateVersion7():N}.tmp";
        try
        {
            using var process = FfmpegExecutable.Create(startInfo => ConfigureExtract(startInfo, sourcePath, temporary, seek));
            FfmpegExecutable.Start(process);
            _ = FfmpegExecutable.CaptureErrors(process);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0 || !File.Exists(temporary) || new FileInfo(temporary).Length == 0)
            {
                return false;
            }

            File.Move(temporary, destination, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public static string FormatTimestamp(TimeSpan value) =>
        value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
}
