using Microsoft.EntityFrameworkCore;
using NidusVision.Core.Models;
using NidusVision.Data;

namespace NidusVision.Tests;

public sealed class TimestampQueryTests : SqliteTestBase
{
    [Fact]
    public async Task QueryWithTimestampFilterOrdersAndFiltersInSql()
    {
        // Arrange
        await using var db = CreateContext();
        var camera = AddCamera(db);
        var now = DateTimeOffset.Parse("2026-09-23T12:00:00Z");
        db.RecordingSegments.AddRange(
            NewSegment(camera.Id, now.AddMinutes(-20), now.AddMinutes(-10)),
            NewSegment(camera.Id, now.AddMinutes(-10), now));
        await db.SaveChangesAsync(CancellationToken.None);

        // Act
        var ordered = await db.RecordingSegments.AsNoTracking()
            .Where(s => s.StartUtc >= now.AddMinutes(-15))
            .OrderByDescending(s => s.StartUtc)
            .ToListAsync(CancellationToken.None);

        // Assert
        ordered.Must().HaveCount(1);
        ordered[0].StartUtc.Must().Be(now.AddMinutes(-10));
    }

    [Fact]
    public async Task SaveChangesAsyncWithOffsetTimestampRoundTripsAsUtc()
    {
        // Arrange
        await using var db = CreateContext();
        var camera = AddCamera(db);
        var start = DateTimeOffset.Parse("2026-09-23T12:00:00+02:00");
        db.DetectionIntervals.Add(new DetectionInterval
        {
            CameraId = camera.Id,
            StartUtc = start,
            EndUtc = start.AddSeconds(5),
            Confidence = 0.9f,
        });
        await db.SaveChangesAsync(CancellationToken.None);

        // Act
        var stored = await db.DetectionIntervals.AsNoTracking()
            .OrderBy(e => e.StartUtc)
            .FirstAsync(CancellationToken.None);

        // Assert
        stored.StartUtc.Offset.Must().Be(TimeSpan.Zero);
        stored.StartUtc.UtcDateTime.Must().Be(start.UtcDateTime);
    }

    private static Camera AddCamera(AppDbContext db)
    {
        var camera = new Camera { Name = "Front", MainRtspUrl = "rtsp://localhost/front" };
        db.Cameras.Add(camera);
        return camera;
    }

    private static RecordingSegment NewSegment(Guid cameraId, DateTimeOffset start, DateTimeOffset end) => new()
    {
        CameraId = cameraId,
        Path = $"{start:yyyyMMddHHmmss}.mp4",
        StartUtc = start,
        EndUtc = end,
    };
}
