using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NidusVision.Core.Models;
using NidusVision.Core.Options;
using NidusVision.Data;
using NidusVision.Streaming;
using NidusVision.Web.Cameras;

namespace NidusVision.Web.Ingest;

public sealed class CameraIngestHostedService(
    IServiceScopeFactory scopes,
    FfmpegSegmentProcess ffmpeg,
    RtspProbe probe,
    ReconnectBackoff backoff,
    CameraStatusTracker statuses,
    ILogger<CameraIngestHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var runners = new Dictionary<Guid, Task>();
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cameras = await db.Cameras.AsNoTracking().Where(c => c.Enabled).ToListAsync(stoppingToken);
            foreach (var camera in cameras)
            {
                if (runners.TryGetValue(camera.Id, out var existing) && !existing.IsCompleted)
                {
                    continue;
                }

                runners[camera.Id] = RunCameraAsync(camera.Id, stoppingToken);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task RunCameraAsync(Guid cameraId, CancellationToken stoppingToken)
    {
        var attempt = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var cameras = scope.ServiceProvider.GetRequiredService<CameraService>();
                var storage = scope.ServiceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;
                var camera = await db.Cameras.FirstOrDefaultAsync(c => c.Id == cameraId, stoppingToken);
                if (camera is null || !camera.Enabled)
                {
                    return;
                }

                var url = cameras.ResolveRtspUrl(camera);
                await RecordStreamMetadataAsync(camera, url, stoppingToken);

                var dir = Path.Combine(Path.GetFullPath(storage.RecordingsDirectory), camera.Id.ToString("N"), DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"), DateTime.UtcNow.ToString("dd"));
                using var process = ffmpeg.Start(
                    url,
                    camera.Transport.ToString(),
                    dir,
                    storage.EffectiveSegmentDurationSeconds);
                var errors = FfmpegExecutable.CaptureErrors(process);
                camera.Status = CameraStatus.Recording;
                await db.SaveChangesAsync(stoppingToken);
                await statuses.SetAsync(camera.Id, CameraStatus.Recording, stoppingToken);
                using var watcher = new FileSystemWatcher(dir, "*.mp4") { EnableRaisingEvents = true };
                watcher.Created += async (_, args) =>
                {
                    try
                    {
                        await Task.Delay(500, stoppingToken);
                        var info = new FileInfo(args.FullPath);
                        if (!info.Exists)
                        {
                            return;
                        }

                        await using var inner = scopes.CreateAsyncScope();
                        var innerDb = inner.ServiceProvider.GetRequiredService<AppDbContext>();
                        var segmentStart = DateTimeOffset.UtcNow;
                        innerDb.RecordingSegments.Add(new RecordingSegment
                        {
                            CameraId = cameraId,
                            Path = args.FullPath,
                            StartUtc = segmentStart,
                            EndUtc = segmentStart.AddSeconds(storage.EffectiveSegmentDurationSeconds),
                            Codec = "copy",
                            ByteSize = info.Length,
                        });
                        await innerDb.SaveChangesAsync(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Failed to index segment {Path}", args.FullPath);
                    }
                };

                await process.WaitForExitAsync(stoppingToken);
                if (process.ExitCode != 0)
                {
                    logger.LogWarning(
                        "FFmpeg exited with {ExitCode} for camera {CameraId}: {Reason}",
                        process.ExitCode,
                        cameraId,
                        errors.Describe("no FFmpeg diagnostics available"));
                }

                camera.Status = CameraStatus.Offline;
                await db.SaveChangesAsync(stoppingToken);
                await statuses.SetAsync(camera.Id, CameraStatus.Offline, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Ingest failed for camera {CameraId}", cameraId);
            }

            attempt++;
            await Task.Delay(backoff.DelayForAttempt(attempt), stoppingToken);
        }
    }

    /// <summary>
    /// Recording runs FFmpeg at warning level, which never prints stream details, so the
    /// resolution and frame rate shown per camera come from a short probe before each connect.
    /// Values are kept when a probe cannot read them so the UI does not lose what it had.
    /// </summary>
    private async Task RecordStreamMetadataAsync(Camera camera, string url, CancellationToken stoppingToken)
    {
        try
        {
            var result = await probe.ProbeAsync(url, camera.Transport.ToString(), stoppingToken);
            camera.LastResolution = result.Resolution ?? camera.LastResolution;
            camera.LastFps = result.Fps ?? camera.LastFps;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Could not read stream metadata for camera {CameraId}", camera.Id);
        }
    }
}
