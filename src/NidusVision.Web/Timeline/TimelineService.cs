using Microsoft.EntityFrameworkCore;
using NidusVision.Core.Models;
using NidusVision.Core.Timeline;
using NidusVision.Data;

namespace NidusVision.Web.Timeline;

public sealed record TimelineRow(Guid CameraId, string CameraName, IReadOnlyList<TimeInterval> Intervals);

public sealed class TimelineService(AppDbContext db)
{
    public async Task<IReadOnlyList<TimelineRow>> GetAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var segments = await db.RecordingSegments.AsNoTracking()
            .Include(s => s.Camera)
            .Where(s => s.EndUtc >= from && s.StartUtc <= to)
            .ToListAsync(cancellationToken);
        var detections = await db.DetectionIntervals.AsNoTracking()
            .Include(e => e.Camera)
            .Where(e => e.EndUtc >= from && e.StartUtc <= to)
            .ToListAsync(cancellationToken);

        return [.. segments.Select(s => (s.CameraId, Name: s.Camera.Name))
            .Concat(detections.Select(e => (e.CameraId, Name: e.Camera.Name)))
            .Distinct()
            .OrderBy(c => c.Name)
            .Select(camera =>
            {
                var continuous = segments
                    .Where(s => s.CameraId == camera.CameraId)
                    .Select(s => new TimeInterval(s.StartUtc, s.EndUtc, false));
                var human = detections
                    .Where(e => e.CameraId == camera.CameraId)
                    .Select(e => new TimeInterval(e.StartUtc, e.EndUtc, true));
                return new TimelineRow(
                    camera.CameraId,
                    camera.Name,
                    TimelineMerger.Merge(continuous.Concat(human)));
            })];
    }
}

internal static class TimelineEndpoints
{
    public static void MapTimelineEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/cameras/timeline", async (DateTimeOffset from, DateTimeOffset to, TimelineService timeline, CancellationToken cancellationToken) =>
            TypedResults.Ok(await timeline.GetAsync(from, to, cancellationToken))).RequireAuthorization();
}
