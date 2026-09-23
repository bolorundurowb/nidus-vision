using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NidusVision.Core.Models;
using NidusVision.Data;

namespace NidusVision.Tests;

public sealed class TimestampQueryTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public TimestampQueryTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Orders_and_filters_segments_by_timestamp_in_sql()
    {
        await using var db = CreateContext();
        var camera = AddCamera(db);
        var now = DateTimeOffset.Parse("2026-09-23T12:00:00Z");
        db.RecordingSegments.AddRange(
            NewSegment(camera.Id, now.AddMinutes(-20), now.AddMinutes(-10)),
            NewSegment(camera.Id, now.AddMinutes(-10), now));
        await db.SaveChangesAsync(CancellationToken.None);

        var ordered = await db.RecordingSegments.AsNoTracking()
            .Where(s => s.StartUtc >= now.AddMinutes(-15))
            .OrderByDescending(s => s.StartUtc)
            .ToListAsync(CancellationToken.None);

        Assert.Equal(now.AddMinutes(-10), Assert.Single(ordered).StartUtc);
    }

    [Fact]
    public async Task Round_trips_timestamps_as_utc()
    {
        await using var db = CreateContext();
        var camera = AddCamera(db);
        var start = DateTimeOffset.Parse("2026-09-23T12:00:00+02:00");
        db.DetectionEvents.Add(new DetectionEvent
        {
            CameraId = camera.Id,
            StartUtc = start,
            EndUtc = start.AddSeconds(5),
            Confidence = 0.9f,
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var stored = await db.DetectionEvents.AsNoTracking()
            .OrderBy(e => e.StartUtc)
            .FirstAsync(CancellationToken.None);

        Assert.Equal(TimeSpan.Zero, stored.StartUtc.Offset);
        Assert.Equal(start.UtcDateTime, stored.StartUtc.UtcDateTime);
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
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
