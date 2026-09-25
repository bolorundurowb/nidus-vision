using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NidusVision.Core.Retention;
using NidusVision.Core.Storage;
using NidusVision.Data;

namespace NidusVision.Data;

public sealed class RetentionWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<RetentionWorker> logger) : BackgroundService
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
        var segments = await db.RecordingSegments.AsNoTracking().ToListAsync(cancellationToken);
        var infos = segments
            .Select(s => new SegmentRetentionInfo(s.Id, s.EndUtc, s.HasHuman, DiskFileSize.Of(s.Path, s.ByteSize), s.Path))
            .ToList();
        var purge = RetentionPlanner.SelectPurge(
            infos,
            time.GetUtcNow(),
            TimeSpan.FromDays(settings.GeneralRetentionDays),
            TimeSpan.FromDays(settings.DetectionRetentionDays),
            settings.MaxStorageBytes);

        foreach (var item in purge)
        {
            var entity = await db.RecordingSegments.FindAsync([item.Id], cancellationToken);
            if (File.Exists(item.Path))
            {
                File.Delete(item.Path);
            }

            DeleteIfExists(RecordingPath.ThumbnailFor(item.Path));
            DeleteIfExists(entity?.ThumbnailPath);

            if (entity is not null)
            {
                db.RecordingSegments.Remove(entity);
            }
        }

        var detectionCutoff = time.GetUtcNow() - TimeSpan.FromDays(settings.DetectionRetentionDays);
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
            purge.Count,
            expiredDetections.Count);
    }

    private static void DeleteIfExists(string? path)
    {
        if (path is not null && File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
