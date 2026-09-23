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
    ReconnectBackoff backoff,
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

                var password = cameras.UnprotectPassword(camera);
                var url = camera.MainRtspUrl;
                if (!string.IsNullOrWhiteSpace(camera.Username) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    url = new UriBuilder(uri) { UserName = camera.Username, Password = password ?? "" }.Uri.ToString();
                }

                var dir = Path.Combine(Path.GetFullPath(storage.RecordingsDirectory), camera.Id.ToString("N"), DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"), DateTime.UtcNow.ToString("dd"));
                using var process = ffmpeg.Start(url, camera.Transport.ToString(), dir);
                camera.Status = CameraStatus.Recording;
                await db.SaveChangesAsync(stoppingToken);
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
                        innerDb.RecordingSegments.Add(new RecordingSegment
                        {
                            CameraId = cameraId,
                            Path = args.FullPath,
                            StartUtc = DateTimeOffset.UtcNow.AddSeconds(-10),
                            EndUtc = DateTimeOffset.UtcNow,
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
                camera.Status = CameraStatus.Offline;
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Ingest failed for camera {CameraId}", cameraId);
            }

            attempt++;
            await Task.Delay(backoff.DelayForAttempt(attempt), stoppingToken);
        }
    }
}
