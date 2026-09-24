using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NidusVision.Core.Models;
using NidusVision.Core.Options;
using NidusVision.Data;

namespace NidusVision.Web.Events;

public sealed class ThumbnailWorker(
    IServiceScopeFactory scopes,
    VideoThumbnailExtractor extractor,
    IOptions<StorageOptions> storage,
    TimeProvider time,
    ILogger<ThumbnailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15), time);
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
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var remaining = VideoThumbnailExtractor.BatchSize;
        remaining -= await FillEventThumbnailsAsync(db, remaining, cancellationToken);
        _ = await FillRecordingThumbnailsAsync(db, remaining, cancellationToken);
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

    private async Task<int> FillEventThumbnailsAsync(AppDbContext db, int limit, CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            return 0;
        }

        var candidates = await db.DetectionEvents
            .Where(e => e.ThumbnailPath == null || e.ThumbnailPath == "")
            .OrderByDescending(e => e.StartUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
        var filled = 0;
        foreach (var detection in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            filled++;
            var source = await ResolveEventSourceAsync(db, detection, cancellationToken);
            if (source is not { } video || !VideoThumbnailExtractor.IsSourceReady(video.Path, time))
            {
                continue;
            }

            var destination = VideoThumbnailExtractor.DestinationForEvent(
                storage.Value.EventsDirectory,
                detection.CameraId,
                detection.StartUtc,
                detection.Id);
            var seek = VideoThumbnailExtractor.SeekForEvent(video.Path, detection, video.SourceStartUtc);
            var written = await extractor.ExtractAsync(video.Path, destination, seek, cancellationToken);
            if (written is null)
            {
                continue;
            }

            detection.ThumbnailPath = written;
        }

        return filled;
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
            if (!VideoThumbnailExtractor.IsSourceReady(segment.Path, time))
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

    private async Task<EventVideoSource?> ResolveEventSourceAsync(
        AppDbContext db,
        DetectionEvent detection,
        CancellationToken cancellationToken)
    {
        if (detection.ClipPath is { Length: > 0 } clipPath && File.Exists(clipPath))
        {
            var clipName = $"{detection.Id:N}.mp4";
            if (string.Equals(Path.GetFileName(clipPath), clipName, StringComparison.OrdinalIgnoreCase))
            {
                return new(clipPath, null);
            }

            var clipSegment = await db.RecordingSegments.AsNoTracking()
                .Where(s => s.Path == clipPath)
                .Select(s => (DateTimeOffset?)s.StartUtc)
                .FirstOrDefaultAsync(cancellationToken);
            return new(clipPath, clipSegment);
        }

        var at = detection.StartUtc.UtcDateTime;
        var recovered = Path.Combine(
            Path.GetFullPath(storage.Value.EventsDirectory),
            detection.CameraId.ToString("N"),
            at.ToString("yyyy"),
            at.ToString("MM"),
            at.ToString("dd"),
            $"{detection.Id:N}.mp4");
        if (File.Exists(recovered))
        {
            return new(recovered, null);
        }

        var segment = await db.RecordingSegments.AsNoTracking()
            .Where(s => s.CameraId == detection.CameraId && s.StartUtc <= detection.EndUtc && s.EndUtc >= detection.StartUtc)
            .OrderBy(s => s.StartUtc)
            .FirstOrDefaultAsync(cancellationToken);
        return segment?.Path is { } path && File.Exists(path)
            ? new(path, segment.StartUtc)
            : null;
    }

    private readonly record struct EventVideoSource(string Path, DateTimeOffset? SourceStartUtc);
}
