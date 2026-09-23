using Microsoft.Extensions.Options;
using NidusVision.Core.Models;
using NidusVision.Core.Options;

namespace NidusVision.Web.Events;

public sealed class EventArtifactStore(IOptions<StorageOptions> options)
{
    public async Task<string?> SaveClipAsync(
        DetectionEvent detection,
        string sourceRecordingPath,
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
        var temporary = destination + ".tmp";
        try
        {
            await using var source = new FileStream(
                sourceRecordingPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 81920,
                useAsync: true);
            await using (var target = new FileStream(
                temporary,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                await source.CopyToAsync(target, cancellationToken);
            }

            File.Move(temporary, destination, overwrite: true);
            return destination;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
