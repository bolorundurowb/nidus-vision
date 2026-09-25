using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NidusVision.Core.Models;
using NidusVision.Core.Options;
using NidusVision.Core.Storage;
using NidusVision.Data;

namespace NidusVision.Web.Events;

public sealed record DetectionIntervalResponse(Guid Id, DateTimeOffset StartUtc, DateTimeOffset EndUtc, float Confidence);
public sealed record RecordingResponse(Guid Id, Guid CameraId, string CameraName, string Location, DateTimeOffset StartUtc, DateTimeOffset EndUtc, long ByteSize, bool HasHuman, bool Available, bool HasThumbnail, string? Resolution, bool IsActive, IReadOnlyList<DetectionIntervalResponse> DetectionIntervals);
internal sealed record RecordingPageRow(Guid Id, Guid CameraId, string CameraName, CameraLocation Location, DateTimeOffset StartUtc, DateTimeOffset EndUtc, long ByteSize, bool HasHuman, bool IsFinalized, string Path, string? ThumbnailPath, string? Resolution);
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
}

public sealed class RecordingLibraryService(AppDbContext db, IOptions<StorageOptions> storage, TimeProvider time)
{
    private const int MaxPageSize = 100;

    public async Task<PagedResponse<RecordingResponse>> SearchRecordingsAsync(
        Guid? cameraId,
        CameraLocation? location,
        int page,
        int pageSize,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        bool? hasHuman,
        CancellationToken cancellationToken)
    {
        await IndexUntrackedRecordingsAsync(cancellationToken);
        (page, pageSize) = NormalizePage(page, pageSize);

        var query = db.RecordingSegments
            .AsNoTracking()
            .Where(segment => (cameraId == null || segment.CameraId == cameraId)
                && (location == null || segment.Camera.Location == location));
        if (fromUtc is { } from)
        {
            query = query.Where(segment => segment.EndUtc >= from);
        }
        if (toUtc is { } to)
        {
            query = query.Where(segment => segment.StartUtc < to);
        }
        if (hasHuman is { } flag)
        {
            query = query.Where(segment => segment.HasHuman == flag);
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
                segment.Camera.Location,
                segment.StartUtc,
                segment.EndUtc,
                segment.ByteSize,
                segment.HasHuman,
                segment.IsFinalized,
                segment.Path,
                segment.ThumbnailPath,
                segment.Camera.LastResolution))
            .ToListAsync(cancellationToken);
        var cameraIds = rows.Select(row => row.CameraId).Distinct().ToArray();
        var earliest = rows.Count == 0 ? default : rows.Min(row => row.StartUtc);
        var latest = rows.Count == 0 ? default : rows.Max(row => row.EndUtc);
        var detections = rows.Count == 0
            ? []
            : await db.DetectionIntervals.AsNoTracking()
                .Where(detection => cameraIds.Contains(detection.CameraId)
                    && detection.EndUtc > earliest
                    && detection.StartUtc < latest)
                .ToListAsync(cancellationToken);
        var now = time.GetUtcNow();
        var items = rows
            .Select(recording =>
            {
                var available = File.Exists(recording.Path);
                var isActive = available && !recording.IsFinalized;
                var endUtc = isActive && now > recording.StartUtc ? now : recording.EndUtc;
                return new RecordingResponse(
                    recording.Id,
                    recording.CameraId,
                    recording.CameraName,
                    recording.Location.ToString(),
                    recording.StartUtc,
                    endUtc,
                    DiskFileSize.Of(recording.Path, recording.ByteSize),
                    recording.HasHuman,
                    available,
                    recording.ThumbnailPath != null && recording.ThumbnailPath != "",
                    recording.Resolution,
                    isActive,
                    [.. detections
                        .Where(detection => detection.CameraId == recording.CameraId
                            && detection.EndUtc > recording.StartUtc
                            && detection.StartUtc < endUtc)
                        .OrderBy(detection => detection.StartUtc)
                        .Select(detection => new DetectionIntervalResponse(
                            detection.Id,
                            detection.StartUtc < recording.StartUtc ? recording.StartUtc : detection.StartUtc,
                            detection.EndUtc > endUtc ? endUtc : detection.EndUtc,
                            detection.Confidence))]);
            })
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

    public async Task<string?> ResolveRecordingThumbnailPathAsync(Guid id, CancellationToken cancellationToken)
    {
        var path = await db.RecordingSegments.AsNoTracking()
            .Where(segment => segment.Id == id)
            .Select(segment => segment.ThumbnailPath)
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
                IsFinalized = true,
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

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize));
}

internal static class RecordingEndpoints
{
    public static void MapRecordingEndpoints(this IEndpointRouteBuilder app)
    {
        var recordings = app.MapGroup("/api/recordings");
        recordings.MapGet("/", async (Guid? cameraId, CameraLocation? location, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, bool? hasHuman, int? page, int? pageSize, RecordingLibraryService library, CancellationToken cancellationToken) =>
            TypedResults.Ok(await library.SearchRecordingsAsync(cameraId, location, page ?? 1, pageSize ?? 12, fromUtc, toUtc, hasHuman, cancellationToken)));
        recordings.MapGet("/{id:guid}/video.mp4", async (Guid id, RecordingLibraryService library, CancellationToken cancellationToken) =>
        {
            var path = await library.ResolveRecordingPathAsync(id, cancellationToken);
            return path is null ? Results.NotFound() : Results.File(path, "video/mp4", enableRangeProcessing: true);
        });
        recordings.MapGet("/{id:guid}/thumbnail", async (Guid id, RecordingLibraryService library, CancellationToken cancellationToken) =>
        {
            var path = await library.ResolveRecordingThumbnailPathAsync(id, cancellationToken);
            return path is null ? Results.NotFound() : Results.File(path, "image/jpeg");
        });
    }
}
