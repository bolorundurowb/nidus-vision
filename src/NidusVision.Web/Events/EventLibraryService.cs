using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NidusVision.Core.Models;
using NidusVision.Core.Options;
using NidusVision.Core.Storage;
using NidusVision.Data;

namespace NidusVision.Web.Events;

public sealed record EventResponse(Guid Id, Guid CameraId, string CameraName, DateTimeOffset StartUtc, DateTimeOffset EndUtc, float Confidence, bool HasThumbnail);
public sealed record RecordingResponse(Guid Id, Guid CameraId, string CameraName, DateTimeOffset StartUtc, DateTimeOffset EndUtc, long ByteSize, bool HasHuman, bool Available);
internal sealed record RecordingPageRow(Guid Id, Guid CameraId, string CameraName, DateTimeOffset StartUtc, DateTimeOffset EndUtc, long ByteSize, bool HasHuman, string Path);
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
}

public sealed class EventLibraryService(AppDbContext db, IOptions<StorageOptions> storage)
{
    private const int MaxPageSize = 100;

    public async Task<PagedResponse<EventResponse>> SearchAsync(
        string? q,
        Guid? cameraId,
        float minConfidence,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        (page, pageSize) = NormalizePage(page, pageSize);
        minConfidence = Math.Clamp(minConfidence, 0, 1);
        var query = db.DetectionEvents
            .AsNoTracking()
            .Where(e => (cameraId == null || e.CameraId == cameraId) && e.Confidence >= minConfidence);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{EscapeLikePattern(q.Trim())}%";
            query = query.Where(e => EF.Functions.Like(e.Camera.Name, pattern, "\\"));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(e => e.StartUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.Id,
                e.CameraId,
                CameraName = e.Camera.Name,
                e.StartUtc,
                e.EndUtc,
                e.Confidence,
                HasThumbnail = e.ThumbnailPath != null && e.ThumbnailPath != "",
            })
            .ToListAsync(cancellationToken);
        return new(
            [.. items.Select(e => new EventResponse(e.Id, e.CameraId, e.CameraName, e.StartUtc, e.EndUtc, e.Confidence, e.HasThumbnail))],
            page,
            pageSize,
            totalCount);
    }

    public async Task<EventResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.DetectionEvents.AsNoTracking().Include(e => e.Camera).FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        return entity is null ? null : ToResponse(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.DetectionEvents.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        if (entity.ThumbnailPath is { } path && File.Exists(path))
        {
            File.Delete(path);
        }
        if (entity.ClipPath is { } clipPath && File.Exists(clipPath))
        {
            File.Delete(clipPath);
        }

        db.DetectionEvents.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<string?> ResolveClipPathAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.DetectionEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        if (entity.ClipPath is { } clipPath && File.Exists(clipPath))
        {
            return clipPath;
        }

        // Recover clips written by versions that created the artifact but did not
        // persist ClipPath (for example, if shutdown happened between those steps).
        var at = entity.StartUtc.UtcDateTime;
        var artifactPath = Path.Combine(
            Path.GetFullPath(storage.Value.EventsDirectory),
            entity.CameraId.ToString("N"),
            at.ToString("yyyy"),
            at.ToString("MM"),
            at.ToString("dd"),
            $"{entity.Id:N}.mp4");
        if (File.Exists(artifactPath))
        {
            return artifactPath;
        }

        // Backward compatibility for events created before dedicated clips were introduced.
        var segment = await db.RecordingSegments.AsNoTracking()
            .Where(s => s.CameraId == entity.CameraId && s.StartUtc <= entity.EndUtc && s.EndUtc >= entity.StartUtc)
            .OrderBy(s => s.StartUtc)
            .FirstOrDefaultAsync(cancellationToken);
        return segment?.Path is { } path && File.Exists(path) ? path : null;
    }

    public async Task<string?> ResolveThumbnailPathAsync(Guid id, CancellationToken cancellationToken)
    {
        var path = await db.DetectionEvents.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => e.ThumbnailPath)
            .FirstOrDefaultAsync(cancellationToken);
        return path is not null && File.Exists(path) ? path : null;
    }

    public async Task<PagedResponse<RecordingResponse>> SearchRecordingsAsync(
        string? q,
        Guid? cameraId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await IndexUntrackedRecordingsAsync(cancellationToken);
        (page, pageSize) = NormalizePage(page, pageSize);

        var query = db.RecordingSegments
            .AsNoTracking()
            .Where(segment => cameraId == null || segment.CameraId == cameraId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{EscapeLikePattern(q.Trim())}%";
            query = query.Where(segment => EF.Functions.Like(segment.Camera.Name, pattern, "\\"));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(segment => segment.StartUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(segment => new RecordingPageRow(
                segment.Id,
                segment.CameraId,
                segment.Camera.Name,
                segment.StartUtc,
                segment.EndUtc,
                segment.ByteSize,
                segment.HasHuman,
                segment.Path))
            .ToListAsync(cancellationToken);
        var items = rows
            .Select(recording => new RecordingResponse(
                recording.Id,
                recording.CameraId,
                recording.CameraName,
                recording.StartUtc,
                recording.EndUtc,
                recording.ByteSize,
                recording.HasHuman,
                File.Exists(recording.Path)))
            .ToList();
        return new(items, page, pageSize, totalCount);
    }

    public async Task<string?> ResolveRecordingPathAsync(Guid id, CancellationToken cancellationToken)
    {
        var path = await db.RecordingSegments.AsNoTracking()
            .Where(segment => segment.Id == id)
            .Select(segment => segment.Path)
            .FirstOrDefaultAsync(cancellationToken);
        return path is not null && File.Exists(path) ? path : null;
    }

    private async Task IndexUntrackedRecordingsAsync(CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(storage.Value.RecordingsDirectory);
        if (!Directory.Exists(root))
        {
            return;
        }

        var cameras = await db.Cameras.AsNoTracking()
            .Select(camera => camera.Id)
            .ToHashSetAsync(cancellationToken);
        var indexedPaths = (await db.RecordingSegments.AsNoTracking()
                .Select(segment => segment.Path)
                .ToListAsync(cancellationToken))
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var path in Directory.EnumerateFiles(root, "*.mp4", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = Path.GetFullPath(path);
            if (indexedPaths.Contains(fullPath) ||
                !TryDescribeRecording(root, fullPath, cameras, out var cameraId, out var start))
            {
                continue;
            }

            var info = new FileInfo(fullPath);
            db.RecordingSegments.Add(new RecordingSegment
            {
                CameraId = cameraId,
                Path = fullPath,
                StartUtc = start,
                EndUtc = start.AddSeconds(storage.Value.EffectiveSegmentDurationSeconds),
                Codec = "copy",
                ByteSize = info.Length,
            });
            indexedPaths.Add(fullPath);
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static bool TryDescribeRecording(
        string root,
        string path,
        HashSet<Guid> cameras,
        out Guid cameraId,
        out DateTimeOffset start)
    {
        cameraId = default;
        start = default;
        var relative = Path.GetRelativePath(root, path);
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Length < 2 ||
            !Guid.TryParseExact(parts[0], "N", out cameraId) ||
            !cameras.Contains(cameraId))
        {
            return false;
        }

        return RecordingPath.TryParseStart(path, out start);
    }

    private static EventResponse ToResponse(DetectionEvent e) =>
        new(e.Id, e.CameraId, e.Camera.Name, e.StartUtc, e.EndUtc, e.Confidence, !string.IsNullOrWhiteSpace(e.ThumbnailPath));

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize));

    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}

internal static class EventEndpoints
{
    public static void MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/events");
        group.MapGet("/", async (string? q, Guid? cameraId, float? minConfidence, int? page, int? pageSize, EventLibraryService events, CancellationToken cancellationToken) =>
            TypedResults.Ok(await events.SearchAsync(q, cameraId, minConfidence ?? 0, page ?? 1, pageSize ?? 12, cancellationToken)));
        group.MapGet("/{id:guid}", async (Guid id, EventLibraryService events, CancellationToken cancellationToken) =>
        {
            var item = await events.GetAsync(id, cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });
        group.MapDelete("/{id:guid}", async (Guid id, EventLibraryService events, CancellationToken cancellationToken) =>
            await events.DeleteAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound());
        group.MapGet("/{id:guid}/clip.mp4", async (Guid id, EventLibraryService events, CancellationToken cancellationToken) =>
        {
            var path = await events.ResolveClipPathAsync(id, cancellationToken);
            return path is null ? Results.NotFound() : Results.File(path, "video/mp4", enableRangeProcessing: true);
        });
        group.MapGet("/{id:guid}/thumbnail", async (Guid id, EventLibraryService events, CancellationToken cancellationToken) =>
        {
            var path = await events.ResolveThumbnailPathAsync(id, cancellationToken);
            return path is null ? Results.NotFound() : Results.File(path, "image/jpeg");
        });

        var recordings = app.MapGroup("/api/recordings");
        recordings.MapGet("/", async (string? q, Guid? cameraId, int? page, int? pageSize, EventLibraryService events, CancellationToken cancellationToken) =>
            TypedResults.Ok(await events.SearchRecordingsAsync(q, cameraId, page ?? 1, pageSize ?? 12, cancellationToken)));
        recordings.MapGet("/{id:guid}/video.mp4", async (Guid id, EventLibraryService events, CancellationToken cancellationToken) =>
        {
            var path = await events.ResolveRecordingPathAsync(id, cancellationToken);
            return path is null ? Results.NotFound() : Results.File(path, "video/mp4", enableRangeProcessing: true);
        });
    }
}
