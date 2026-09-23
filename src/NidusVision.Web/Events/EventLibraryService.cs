using Microsoft.EntityFrameworkCore;
using NidusVision.Core.Events;
using NidusVision.Core.Models;
using NidusVision.Data;

namespace NidusVision.Web.Events;

public sealed record EventResponse(Guid Id, Guid CameraId, string CameraName, DateTimeOffset StartUtc, DateTimeOffset EndUtc, float Confidence, string? ThumbnailPath);

public sealed class EventLibraryService(AppDbContext db)
{
    public async Task<IReadOnlyList<EventResponse>> SearchAsync(string? q, Guid? cameraId, float minConfidence, CancellationToken cancellationToken)
    {
        var events = await db.DetectionEvents.AsNoTracking().Include(e => e.Camera).OrderByDescending(e => e.StartUtc).ToListAsync(cancellationToken);
        return [.. events
            .Where(e => EventSearchFilter.Matches($"{e.Camera.Name} {e.Confidence}", q, e.CameraId, cameraId, e.Confidence, minConfidence))
            .Select(ToResponse)];
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

        var segment = await db.RecordingSegments.AsNoTracking()
            .Where(s => s.CameraId == entity.CameraId && s.StartUtc <= entity.EndUtc && s.EndUtc >= entity.StartUtc)
            .OrderBy(s => s.StartUtc)
            .FirstOrDefaultAsync(cancellationToken);
        return segment?.Path is { } path && File.Exists(path) ? path : null;
    }

    private static EventResponse ToResponse(DetectionEvent e) =>
        new(e.Id, e.CameraId, e.Camera.Name, e.StartUtc, e.EndUtc, e.Confidence, e.ThumbnailPath);
}

internal static class EventEndpoints
{
    public static void MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/events");
        group.MapGet("/", async (string? q, Guid? cameraId, float? minConfidence, EventLibraryService events, CancellationToken cancellationToken) =>
            TypedResults.Ok(await events.SearchAsync(q, cameraId, minConfidence ?? 0, cancellationToken)));
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
    }
}
