using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NidusVision.Core.Options;
using NidusVision.Core.Retention;
using NidusVision.Core.Storage;
using NidusVision.Data;

namespace NidusVision.Data;

public sealed class RetentionWorker(
    IServiceScopeFactory scopes,
    TimeProvider time,
    IOptions<StorageOptions> storage,
    ILogger<RetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Retention cycle failed.");
            }
        }
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.AppSettings.AsNoTracking().OrderBy(s => s.Id).FirstAsync(cancellationToken);
        var now = time.GetUtcNow();
        // Clamp here too: rows saved before the Settings API enforced limits could hold values
        // large enough to overflow the cutoff arithmetic, which would stop every cycle.
        var generalDays = RetentionLimits.ClampDays(settings.GeneralRetentionDays);
        var detectionDays = RetentionLimits.ClampDays(settings.DetectionRetentionDays);
        var maxStorageBytes = settings.MaxStorageBytes is { } cap ? Math.Max(cap, RetentionLimits.MinStorageBytes) : (long?)null;
        var segments = await db.RecordingSegments.AsNoTracking().ToListAsync(cancellationToken);
        var activeSince = now - TimeSpan.FromSeconds(storage.Value.EffectiveSegmentDurationSeconds * 2.0);
        var infos = segments
            // FFmpeg is still writing a recent unfinalized segment. Leave it for the next cycle.
            // An older unfinalized row is a leftover from a crash, and normal rules apply to it.
            .Where(s => s.IsFinalized || s.StartUtc < activeSince)
            .Select(s => new SegmentRetentionInfo(s.Id, s.EndUtc, s.HasHuman, DiskFileSize.Of(s.Path, s.ByteSize), s.Path))
            .ToList();
        var purge = RetentionPlanner.SelectPurge(
            infos,
            now,
            TimeSpan.FromDays(generalDays),
            TimeSpan.FromDays(detectionDays),
            maxStorageBytes);

        var removed = 0;
        foreach (var item in purge)
        {
            var entity = await db.RecordingSegments.FindAsync([item.Id], cancellationToken);
            try
            {
                DeleteIfExists(item.Path);
                DeleteIfExists(RecordingPath.ThumbnailFor(item.Path));
                DeleteIfExists(entity?.ThumbnailPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Keep the row so the next cycle retries, and carry on with the rest.
                logger.LogWarning(ex, "Retention could not delete {Path}. It will be retried.", item.Path);
                continue;
            }

            if (entity is not null)
            {
                db.RecordingSegments.Remove(entity);
                removed++;
            }
        }

        var detectionCutoff = now - TimeSpan.FromDays(detectionDays);
        var expiredDetections = await db.DetectionIntervals
            .Where(e => e.EndUtc <= detectionCutoff)
            .ToListAsync(cancellationToken);
        foreach (var detection in expiredDetections)
        {
            db.DetectionIntervals.Remove(detection);
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Retention removed {SegmentCount} segments and {DetectionCount} detection intervals.",
            removed,
            expiredDetections.Count);
    }

    private void DeleteIfExists(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            return;
        }

        if (!StorageRoot.Contains(storage.Value.RecordingsDirectory, path))
        {
            logger.LogWarning("Retention skipped {Path} because it is outside the recordings directory.", path);
            return;
        }

        File.Delete(path);
    }
}
