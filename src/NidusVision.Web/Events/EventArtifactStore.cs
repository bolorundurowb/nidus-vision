using Microsoft.Extensions.Options;
using NidusVision.Core.Models;
using NidusVision.Core.Options;
using NidusVision.Streaming;

namespace NidusVision.Web.Events;

public sealed class EventArtifactStore(IOptions<StorageOptions> options)
{
    private static readonly TimeSpan LeadIn = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MaxDuration = TimeSpan.FromSeconds(30);

    public async Task<string?> SaveClipAsync(
        DetectionEvent detection,
        string sourceRecordingPath,
        DateTimeOffset sourceStartUtc,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(sourceRecordingPath))
        {
            return null;
        }

        var at = detection.StartUtc.UtcDateTime;
        var directory = Path.Combine(
            Path.GetFullPath(options.Value.EventsDirectory),
            detection.CameraId.ToString("N"),
            at.ToString("yyyy"),
            at.ToString("MM"),
            at.ToString("dd"));
        Directory.CreateDirectory(directory);

        var destination = Path.Combine(directory, $"{detection.Id:N}.mp4");
        if (await TryExtractSnippetAsync(sourceRecordingPath, sourceStartUtc, detection.StartUtc, destination, cancellationToken))
        {
            return destination;
        }

        return sourceRecordingPath;
    }

    private static async Task<bool> TryExtractSnippetAsync(
        string sourceRecordingPath,
        DateTimeOffset sourceStartUtc,
        DateTimeOffset detectionStartUtc,
        string destination,
        CancellationToken cancellationToken)
    {
        if (!FfmpegExecutable.IsAvailable())
        {
            return false;
        }

        var seek = detectionStartUtc - sourceStartUtc - LeadIn;
        if (seek < TimeSpan.Zero)
        {
            seek = TimeSpan.Zero;
        }

        var temporary = destination + ".tmp";
        try
        {
            using var process = FfmpegExecutable.Create(startInfo =>
            {
                startInfo.ArgumentList.Add("-hide_banner");
                startInfo.ArgumentList.Add("-loglevel");
                startInfo.ArgumentList.Add("error");
                startInfo.ArgumentList.Add("-ss");
                startInfo.ArgumentList.Add(FormatTimestamp(seek));
                startInfo.ArgumentList.Add("-i");
                startInfo.ArgumentList.Add(sourceRecordingPath);
                startInfo.ArgumentList.Add("-t");
                startInfo.ArgumentList.Add(FormatTimestamp(MaxDuration));
                startInfo.ArgumentList.Add("-c");
                startInfo.ArgumentList.Add("copy");
                startInfo.ArgumentList.Add("-an");
                startInfo.ArgumentList.Add("-y");
                startInfo.ArgumentList.Add(temporary);
            });
            FfmpegExecutable.Start(process);
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

    private static string FormatTimestamp(TimeSpan value) =>
        value.ToString(@"hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture);
}
