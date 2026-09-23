using Microsoft.EntityFrameworkCore;
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

        return [.. segments
            .GroupBy(s => new { s.CameraId, s.Camera.Name })
            .Select(g => new TimelineRow(
                g.Key.CameraId,
                g.Key.Name,
                TimelineMerger.Merge(g.Select(s => new TimeInterval(s.StartUtc, s.EndUtc, s.HasHuman)))))];
    }
}

internal static class TimelineEndpoints
{
    public static void MapTimelineEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/cameras/timeline", async (DateTimeOffset from, DateTimeOffset to, TimelineService timeline, CancellationToken cancellationToken) =>
            TypedResults.Ok(await timeline.GetAsync(from, to, cancellationToken)));
}
