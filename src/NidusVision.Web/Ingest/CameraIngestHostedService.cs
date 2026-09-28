using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NidusVision.Core.Cameras;
using NidusVision.Core.Ingest;
using NidusVision.Core.Inference;
using NidusVision.Core.Models;
using NidusVision.Core.Options;
using NidusVision.Core.Storage;
using NidusVision.Data;
using NidusVision.Inference;
using NidusVision.Streaming;
using NidusVision.Web.Cameras;
using NidusVision.Web.Inference;

namespace NidusVision.Web.Ingest;

public sealed class CameraIngestHostedService(
    IServiceScopeFactory scopes,
    FfmpegSegmentProcess ffmpeg,
    RtspProbe probe,
    ReconnectBackoff backoff,
    CameraStatusTracker statuses,
    DetectionFrameBroker frames,
    IHumanDetector detector,
    ILogger<CameraIngestHostedService> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<Guid, CameraRun> _runners = new();
    private readonly Lock _collisionGate = new();
    private readonly HashSet<string> _collisions = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await SortExistingFootageAsync(stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var cameras = await db.Cameras.AsNoTracking().ToListAsync(stoppingToken);
                var settings = await db.AppSettings.AsNoTracking().OrderBy(s => s.Id).FirstAsync(stoppingToken);
                var emitFrames = settings.InferenceEnabled && detector.IsAvailable;
                var recording = cameras.Where(camera => camera.Enabled && camera.RecordingEnabled).ToDictionary(camera => camera.Id);

                foreach (var id in _runners.Keys)
                {
                    if (!recording.ContainsKey(id))
                    {
                        CancelRun(id);
                    }
                }

                foreach (var camera in cameras)
                {
                    var shouldRecord = recording.ContainsKey(camera.Id);
                    var fingerprint = CameraIngestFingerprint.From(camera, emitFrames, settings.SampleFps);
                    if (_runners.TryGetValue(camera.Id, out var existing))
                    {
                        if (existing.Task.IsCompleted)
                        {
                            RemoveRun(camera.Id);
                        }
                        else if (!shouldRecord || !string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal))
                        {
                            existing.Cts.Cancel();
                            continue;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    if (!shouldRecord)
                    {
                        var recordingsRoot = scope.ServiceProvider.GetRequiredService<IOptions<StorageOptions>>().Value.RecordingsDirectory;
                        try
                        {
                            await AlignCameraFolderAsync(db, camera, recordingsRoot, stoppingToken);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            logger.LogWarning(ex, "Could not arrange recordings for camera {CameraId}.", camera.Id);
                        }

                        continue;
                    }

                    var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    var run = new CameraRun(fingerprint, cts);
                    run.Task = RunCameraAsync(camera.Id, emitFrames, settings.SampleFps, cts.Token, stoppingToken);
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

    private async Task RunCameraAsync(
        Guid cameraId,
        bool emitFrames,
        float sampleFps,
        CancellationToken stoppingToken,
        CancellationToken hostStoppingToken)
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
                if (camera is null || !camera.Enabled || !camera.RecordingEnabled)
                {
                    return;
                }

                var url = cameras.ResolveRtspUrl(camera);
                await RecordStreamMetadataAsync(camera, url, stoppingToken);
                await FinalizeOpenSegmentsAsync(db, storage.RecordingsDirectory, cameraId, DateTimeOffset.UtcNow, stoppingToken);
                var cameraRoot = await AlignCameraFolderAsync(db, camera, storage.RecordingsDirectory, stoppingToken);
                process = ffmpeg.Start(
                    url,
                    camera.Transport.ToString(),
                    cameraRoot,
                    storage.EffectiveSegmentDurationSeconds,
                    emitFrames ? sampleFps : null);
                var errors = FfmpegExecutable.CaptureErrors(process);
                camera.Status = CameraStatus.Recording;
                await db.SaveChangesAsync(stoppingToken);
                await statuses.SetAsync(camera.Id, CameraStatus.Recording, stoppingToken);

                var created = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
                using var watcher = new FileSystemWatcher(cameraRoot, "*.mp4")
                {
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true,
                };
                watcher.Created += (_, args) => created.Writer.TryWrite(args.FullPath);
                var indexTask = IndexSegmentsAsync(
                    cameraId,
                    storage.EffectiveSegmentDurationSeconds,
                    created.Reader,
                    stoppingToken);
                var stdoutTask = emitFrames
                    ? PumpDetectionFramesAsync(process, camera.Id, camera.LastResolution, stoppingToken)
                    : DiscardStdoutAsync(process, stoppingToken);

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

                    try
                    {
                        await stdoutTask;
                    }
                    catch (OperationCanceledException)
                    {
                        /* stdout pump stopped with the camera */
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
                await MarkOfflineAsync(cameraId);
            }
            catch (OperationCanceledException)
            {
                KillIfRunning(process);
                frames.Drop(cameraId);
                await FinalizeOpenSegmentsAsync(cameraId, DateTimeOffset.UtcNow, CancellationToken.None);
                if (!await RecordingStoppedWhileCameraStaysEnabledAsync(cameraId, hostStoppingToken))
                {
                    await MarkOfflineAsync(cameraId);
                }

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

    private async Task PumpDetectionFramesAsync(
        Process process,
        Guid cameraId,
        string? resolution,
        CancellationToken stoppingToken)
    {
        var sourceWidth = LetterboxTransform.DefaultInputSize;
        var sourceHeight = LetterboxTransform.DefaultInputSize;
        if (StreamDimensions.TryParse(resolution, out var parsedWidth, out var parsedHeight))
        {
            sourceWidth = parsedWidth;
            sourceHeight = parsedHeight;
        }

        var buffer = new byte[FfmpegSegmentProcess.DetectionFrameBytes];
        var stream = process.StandardOutput.BaseStream;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var read = 0;
                while (read < buffer.Length)
                {
                    var n = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read), stoppingToken);
                    if (n == 0)
                    {
                        return;
                    }

                    read += n;
                }

                var copy = new byte[buffer.Length];
                Buffer.BlockCopy(buffer, 0, copy, 0, buffer.Length);
                frames.Publish(new DetectionFrame(
                    cameraId,
                    DateTimeOffset.UtcNow,
                    copy,
                    FfmpegSegmentProcess.DetectionInputSize,
                    FfmpegSegmentProcess.DetectionInputSize,
                    sourceWidth,
                    sourceHeight));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Stopped reading detection frames for camera {CameraId}.", cameraId);
        }
    }

    private static async Task DiscardStdoutAsync(Process process, CancellationToken stoppingToken)
    {
        try
        {
            await process.StandardOutput.BaseStream.CopyToAsync(Stream.Null, stoppingToken);
        }
        catch (IOException)
        {
            /* process exited */
        }
        catch (OperationCanceledException)
        {
            /* cancelled */
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
                    var nextStart = RecordingPath.TryParseStart(path, out var parsed) ? parsed : DateTimeOffset.UtcNow;
                    await FinalizeSegmentAsync(cameraId, previous, nextStart, CancellationToken.None);
                }

                await InsertSegmentAsync(cameraId, path, segmentDurationSeconds, CancellationToken.None);
                previous = path;
            }
        }
        finally
        {
            if (previous is not null)
            {
                await FinalizeSegmentAsync(cameraId, previous, DateTimeOffset.UtcNow, CancellationToken.None);
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
            var segment = new RecordingSegment
            {
                CameraId = cameraId,
                Path = fullPath,
                StartUtc = start,
                EndUtc = start.AddSeconds(segmentDurationSeconds),
                Codec = "copy",
                ByteSize = 0,
                IsFinalized = false,
            };
            db.RecordingSegments.Add(segment);
            await LinkOverlappingDetectionsAsync(db, segment, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to index segment {Path}", path);
        }
    }

    private async Task FinalizeOpenSegmentsAsync(Guid cameraId, DateTimeOffset endUtc, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recordings = scope.ServiceProvider.GetRequiredService<IOptions<StorageOptions>>().Value.RecordingsDirectory;
        await FinalizeOpenSegmentsAsync(db, recordings, cameraId, endUtc, cancellationToken);
    }

    private async Task FinalizeOpenSegmentsAsync(
        AppDbContext db,
        string recordingsDirectory,
        Guid cameraId,
        DateTimeOffset endUtc,
        CancellationToken cancellationToken)
    {
        var segments = await db.RecordingSegments
            .Where(segment => segment.CameraId == cameraId)
            .ToListAsync(cancellationToken);
        var open = segments.Where(segment => !segment.IsFinalized).ToList();
        foreach (var segment in open)
        {
            ApplyFinalize(segment, endUtc);
            try
            {
                var claim = PlaceFinalized(recordingsDirectory, segment, segments);
                if (claim.DropDuplicate)
                {
                    db.RecordingSegments.Remove(segment);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Could not place finalized recording {Path} for camera {CameraId}. Leaving it in place.", segment.Path, cameraId);
            }
        }

        if (open.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            RemoveEmptiedDirectories(recordingsDirectory, cameraId, open);
        }
    }

    private async Task FinalizeSegmentAsync(Guid cameraId, string path, DateTimeOffset endUtc, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var recordings = scope.ServiceProvider.GetRequiredService<IOptions<StorageOptions>>().Value.RecordingsDirectory;
            var fullPath = Path.GetFullPath(path);
            var segment = await db.RecordingSegments.FirstOrDefaultAsync(s => s.Path == fullPath, cancellationToken);
            if (segment is null)
            {
                return;
            }

            var siblings = await db.RecordingSegments
                .Where(item => item.CameraId == cameraId)
                .ToListAsync(cancellationToken);
            ApplyFinalize(segment, endUtc);
            var claim = PlaceFinalized(recordings, segment, siblings);
            if (claim.DropDuplicate)
            {
                db.RecordingSegments.Remove(segment);
            }

            await db.SaveChangesAsync(cancellationToken);
            RemoveEmptiedDirectories(recordings, cameraId, [segment]);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to finalize segment {Path}", path);
        }
    }

    private async Task SortExistingFootageAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recordings = scope.ServiceProvider.GetRequiredService<IOptions<StorageOptions>>().Value.RecordingsDirectory;
        var cameras = await db.Cameras.ToListAsync(cancellationToken);
        foreach (var camera in cameras)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string directory;
            try
            {
                directory = await AlignCameraFolderAsync(db, camera, recordings, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Could not arrange recordings for camera {CameraId}.", camera.Id);
                continue;
            }
            var segments = await db.RecordingSegments.Where(segment => segment.CameraId == camera.Id).ToListAsync(cancellationToken);
            var changed = false;
            foreach (var segment in segments.Where(segment => segment.IsFinalized).ToList())
            {
                try
                {
                    var claim = PlaceFinalized(recordings, segment, segments);
                    if (claim.DropDuplicate)
                    {
                        db.RecordingSegments.Remove(segment);
                        changed = true;
                    }
                    else if (claim.Changed)
                    {
                        changed = true;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Could not sort recording {Path} for camera {CameraId}. Leaving it in place.", segment.Path, camera.Id);
                }
            }

            if (changed)
            {
                await db.SaveChangesAsync(cancellationToken);
            }

            RecordingLayout.RemoveEmptyChildDirectories(directory, segments.Where(segment => !segment.IsFinalized).Select(segment => segment.Path));
        }
    }

    private async Task<string> AlignCameraFolderAsync(
        AppDbContext db,
        Camera camera,
        string recordingsDirectory,
        CancellationToken cancellationToken)
    {
        var choice = RecordingLayout.Resolve(
            recordingsDirectory,
            camera.Id,
            camera.Name,
            message => logger.LogWarning("{Message}", message));
        var segments = await db.RecordingSegments.Where(segment => segment.CameraId == camera.Id).ToListAsync(cancellationToken);
        var changed = false;
        foreach (var segment in segments)
        {
            changed |= Retarget(segment.Path, recordingsDirectory, choice.Directory, path => segment.Path = path);
            changed |= Retarget(segment.ThumbnailPath, recordingsDirectory, choice.Directory, path => segment.ThumbnailPath = path);
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return choice.Directory;
    }

    private bool Retarget(string? storedPath, string recordingsDirectory, string cameraDirectory, Action<string> assign)
    {
        var candidate = RecordingLayout.RetargetPath(storedPath, recordingsDirectory, cameraDirectory);
        if (candidate is null || storedPath is null || !RecordingLayout.ShouldRetarget(storedPath, candidate))
        {
            if (candidate is not null && storedPath is not null && File.Exists(storedPath) && File.Exists(candidate) && NoteCollision(candidate))
            {
                logger.LogWarning(
                    "Left {Path} in place because {Destination} is a different file.",
                    storedPath,
                    candidate);
            }

            return false;
        }

        if (File.Exists(storedPath) && File.Exists(candidate))
        {
            try
            {
                File.Delete(storedPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not remove duplicate recording {Path}.", storedPath);
            }
        }

        assign(candidate);
        return true;
    }

    private RecordingPathClaim PlaceFinalized(
        string recordingsDirectory,
        RecordingSegment segment,
        IReadOnlyList<RecordingSegment> siblings)
    {
        var cameraDirectory = RecordingLayout.CameraDirectoryFor(recordingsDirectory, segment.Path);
        if (cameraDirectory is null)
        {
            return default;
        }

        var placed = RecordingLayout.Place(cameraDirectory, segment.Path, segment.ThumbnailPath, segment.IsFinalized);
        if (placed.Result == RecordingPlaceResult.Collided && NoteCollision(placed.Path))
        {
            logger.LogWarning(
                "Left {Path} in place because the day folder already contains a different recording with that name.",
                placed.Path);
        }

        if (placed.Result is not (RecordingPlaceResult.Moved or RecordingPlaceResult.Adopted))
        {
            return default;
        }

        var claim = RecordingLayout.ClaimIndexedPath(segment, placed.Path, placed.ThumbnailPath, siblings);
        if (claim.DropDuplicate)
        {
            logger.LogWarning(
                "Dropped duplicate recording {Path} because {Destination} is already indexed.",
                segment.Path,
                placed.Path);
        }

        return claim;
    }

    private bool NoteCollision(string path)
    {
        lock (_collisionGate)
        {
            return _collisions.Add(path);
        }
    }

    private void RemoveEmptiedDirectories(string recordingsDirectory, Guid cameraId, IEnumerable<RecordingSegment> keep)
    {
        var sample = keep.Select(segment => segment.Path).FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
        if (sample is null)
        {
            return;
        }

        var directory = RecordingLayout.CameraDirectoryFor(recordingsDirectory, sample);
        if (directory is null)
        {
            return;
        }

        RecordingLayout.RemoveEmptyChildDirectories(
            directory,
            keep.Where(segment => !segment.IsFinalized).Select(segment => segment.Path));
    }

    private async Task<bool> RecordingStoppedWhileCameraStaysEnabledAsync(Guid cameraId, CancellationToken hostStoppingToken)
    {
        if (hostStoppingToken.IsCancellationRequested)
        {
            return false;
        }

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var camera = await db.Cameras.AsNoTracking().FirstOrDefaultAsync(item => item.Id == cameraId);
            return camera is { Enabled: true, RecordingEnabled: false };
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not read camera {CameraId} after recording stopped.", cameraId);
            return false;
        }
    }

    private static void ApplyFinalize(RecordingSegment segment, DateTimeOffset endUtc)
    {
        segment.ByteSize = DiskFileSize.Of(segment.Path, segment.ByteSize);
        if (endUtc > segment.StartUtc)
        {
            segment.EndUtc = endUtc;
        }

        segment.IsFinalized = true;
    }

    internal static async Task LinkOverlappingDetectionsAsync(
        AppDbContext db,
        RecordingSegment segment,
        CancellationToken cancellationToken)
    {
        var detections = await db.DetectionIntervals
            .Where(detection => detection.CameraId == segment.CameraId)
            .ToListAsync(cancellationToken);
        var overlapping = detections
            .Where(detection => DetectionOverlap.Overlaps(segment.StartUtc, segment.EndUtc, detection.StartUtc, detection.EndUtc))
            .ToList();
        if (overlapping.Count == 0)
        {
            return;
        }

        segment.HasHuman = true;
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
