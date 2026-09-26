using Microsoft.EntityFrameworkCore;
using NidusVision.Data;

namespace NidusVision.Web.Events;

public sealed class ThumbnailWorker(
    IServiceScopeFactory scopes,
    VideoThumbnailExtractor extractor,
    ILogger<ThumbnailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(ex, "Thumbnail cycle skipped.");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<RecordingLibraryService>().IndexUntrackedRecordingsAsync(cancellationToken);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _ = await FillRecordingThumbnailsAsync(db, VideoThumbnailExtractor.BatchSize, cancellationToken);
        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public static IReadOnlyList<T> SelectMissing<T>(IEnumerable<T> items, Func<T, string?> pathSelector, int limit)
    {
        var selected = new List<T>(Math.Max(0, limit));
        foreach (var item in items)
        {
            if (!VideoThumbnailExtractor.NeedsThumbnail(pathSelector(item)))
            {
                continue;
            }

            selected.Add(item);
            if (selected.Count >= limit)
            {
                break;
            }
        }

        return selected;
    }

    private async Task<int> FillRecordingThumbnailsAsync(AppDbContext db, int limit, CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            return 0;
        }

        var candidates = await db.RecordingSegments
            .Where(s => s.ThumbnailPath == null || s.ThumbnailPath == "")
            .OrderByDescending(s => s.StartUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
        var filled = 0;
        foreach (var segment in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            filled++;
            if (!VideoThumbnailExtractor.IsSourceReady(segment.Path))
            {
                continue;
            }

            var destination = VideoThumbnailExtractor.DestinationForRecording(segment.Path);
            var written = await extractor.ExtractAsync(segment.Path, destination, VideoThumbnailExtractor.RecordingSeek, cancellationToken);
            if (written is null)
            {
                continue;
            }

            segment.ThumbnailPath = written;
        }

        return filled;
    }

}
