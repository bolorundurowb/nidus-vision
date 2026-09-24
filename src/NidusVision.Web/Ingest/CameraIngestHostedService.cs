using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NidusVision.Core.Ingest;
using NidusVision.Core.Models;
using NidusVision.Core.Options;
using NidusVision.Core.Storage;
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
    private readonly ConcurrentDictionary<Guid, CameraRun> _runners = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var cameras = await db.Cameras.AsNoTracking().Where(c => c.Enabled).ToListAsync(stoppingToken);
                var enabled = cameras.ToDictionary(c => c.Id);

                foreach (var id in _runners.Keys)
                {
                    if (!enabled.ContainsKey(id))
                    {
                        CancelRun(id);
                    }
                }

                foreach (var camera in cameras)
                {
                    var fingerprint = CameraIngestFingerprint.From(camera);
                    if (_runners.TryGetValue(camera.Id, out var existing))
                    {
                        if (existing.Task.IsCompleted)
                        {
                            RemoveRun(camera.Id);
                        }
                        else if (!string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal))
                        {
                            existing.Cts.Cancel();
                            continue;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    var run = new CameraRun(fingerprint, cts);
                    run.Task = RunCameraAsync(camera.Id, cts.Token);
                    _runners[camera.Id] = run;
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            /* host is stopping */
        }
        finally
        {
            foreach (var id in _runners.Keys)
            {
                CancelRun(id);
            }

            try
            {
                await Task.WhenAll(_runners.Values.Select(run => run.Task));
            }
            catch (OperationCanceledException)
            {
                /* expected when camera runs stop */
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var id in _runners.Keys)
        {
            CancelRun(id);
        }

        await base.StopAsync(cancellationToken);
        foreach (var run in _runners.Values)
        {
            run.Dispose();
        }

        _runners.Clear();
    }

    private void CancelRun(Guid cameraId)
    {
        if (_runners.TryGetValue(cameraId, out var run))
        {
            run.Cts.Cancel();
        }
    }

    private void RemoveRun(Guid cameraId)
    {
        if (_runners.TryRemove(cameraId, out var run))
        {
            run.Dispose();
        }
    }

    private async Task RunCameraAsync(Guid cameraId, CancellationToken stoppingToken)
    {
        var attempt = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            Process? process = null;
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

                var cameraRoot = Path.Combine(Path.GetFullPath(storage.RecordingsDirectory), camera.Id.ToString("N"));
                Directory.CreateDirectory(cameraRoot);
                process = ffmpeg.Start(
                    url,
                    camera.Transport.ToString(),
                    cameraRoot,
                    storage.EffectiveSegmentDurationSeconds);
                var errors = FfmpegExecutable.CaptureErrors(process);
                camera.Status = CameraStatus.Recording;
                await db.SaveChangesAsync(stoppingToken);
                await statuses.SetAsync(camera.Id, CameraStatus.Recording, stoppingToken);

                var created = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
                using var watcher = new FileSystemWatcher(cameraRoot, "*.mp4")
                {
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true,
                };
                watcher.Created += (_, args) => created.Writer.TryWrite(args.FullPath);
                var indexTask = IndexSegmentsAsync(
                    cameraId,
                    storage.EffectiveSegmentDurationSeconds,
                    created.Reader,
                    stoppingToken);

                try
                {
                    await process.WaitForExitAsync(stoppingToken);
                }
                finally
                {
                    KillIfRunning(process);
                    created.Writer.TryComplete();
                    try
                    {
                        await indexTask;
                    }
                    catch (OperationCanceledException)
                    {
                        /* camera stopped while a segment was being indexed */
                    }
                }

                if (!stoppingToken.IsCancellationRequested && process.ExitCode != 0)
                {
                    logger.LogWarning(
                        "FFmpeg exited with {ExitCode} for camera {CameraId}: {Reason}",
                        process.ExitCode,
                        cameraId,
                        errors.Describe("no FFmpeg diagnostics available"));
                }

                camera.Status = CameraStatus.Offline;
                await db.SaveChangesAsync(CancellationToken.None);
                await statuses.SetAsync(camera.Id, CameraStatus.Offline, CancellationToken.None);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Ingest failed for camera {CameraId}", cameraId);
                KillIfRunning(process);
            }
            catch (OperationCanceledException)
            {
                KillIfRunning(process);
                await MarkOfflineAsync(cameraId);
                return;
            }
            finally
            {
                process?.Dispose();
            }

            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            attempt++;
            await Task.Delay(backoff.DelayForAttempt(attempt), stoppingToken);
        }
    }

    private async Task IndexSegmentsAsync(
        Guid cameraId,
        int segmentDurationSeconds,
        ChannelReader<string> created,
        CancellationToken stoppingToken)
    {
        string? previous = null;
        try
        {
            await foreach (var path in created.ReadAllAsync(stoppingToken))
            {
                if (previous is not null &&
                    !string.Equals(previous, path, StringComparison.OrdinalIgnoreCase))
                {
                    await FinalizeSegmentAsync(previous, CancellationToken.None);
                }

                await InsertSegmentAsync(cameraId, path, segmentDurationSeconds, CancellationToken.None);
                previous = path;
            }
        }
        finally
        {
            if (previous is not null)
            {
                await FinalizeSegmentAsync(previous, CancellationToken.None);
            }
        }
    }

    private async Task InsertSegmentAsync(
        Guid cameraId,
        string path,
        int segmentDurationSeconds,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var fullPath = Path.GetFullPath(path);
            if (await db.RecordingSegments.AnyAsync(s => s.Path == fullPath, cancellationToken))
            {
                return;
            }

            var start = RecordingPath.TryParseStart(fullPath, out var parsed)
                ? parsed
                : DateTimeOffset.UtcNow;
            db.RecordingSegments.Add(new RecordingSegment
            {
                CameraId = cameraId,
                Path = fullPath,
                StartUtc = start,
                EndUtc = start.AddSeconds(segmentDurationSeconds),
                Codec = "copy",
                ByteSize = 0,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to index segment {Path}", path);
        }
    }

    private async Task FinalizeSegmentAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var fullPath = Path.GetFullPath(path);
            var segment = await db.RecordingSegments.FirstOrDefaultAsync(s => s.Path == fullPath, cancellationToken);
            if (segment is null)
            {
                return;
            }

            segment.ByteSize = DiskFileSize.Of(fullPath, segment.ByteSize);
            var end = DateTimeOffset.UtcNow;
            if (end > segment.StartUtc)
            {
                segment.EndUtc = end;
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to finalize segment {Path}", path);
        }
    }

    private async Task MarkOfflineAsync(Guid cameraId)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var camera = await db.Cameras.FirstOrDefaultAsync(c => c.Id == cameraId);
            if (camera is not null)
            {
                camera.Status = CameraStatus.Offline;
                await db.SaveChangesAsync();
            }

            await statuses.SetAsync(cameraId, CameraStatus.Offline, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not mark camera {CameraId} offline after cancel.", cameraId);
        }
    }

    private static void KillIfRunning(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(TimeSpan.FromSeconds(5));
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            /* already exited or cannot be killed */
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

    private sealed class CameraRun(string fingerprint, CancellationTokenSource cts) : IDisposable
    {
        public string Fingerprint { get; } = fingerprint;
        public CancellationTokenSource Cts { get; } = cts;
        public Task Task { get; set; } = Task.CompletedTask;

        public void Dispose() => Cts.Dispose();
    }
}
