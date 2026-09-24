using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NidusVision.Core.Models;
using NidusVision.Core.Options;
using NidusVision.Data;
using NidusVision.Web.Events;

namespace NidusVision.Tests;

public sealed class EventResponseTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public EventResponseTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Event_response_exposes_thumbnail_flag_not_filesystem_path()
    {
        await using var db = CreateContext();
        var camera = new Camera { Name = "Front", MainRtspUrl = "rtsp://cam/stream" };
        db.Cameras.Add(camera);
        db.DetectionEvents.Add(new DetectionEvent
        {
            CameraId = camera.Id,
            StartUtc = DateTimeOffset.Parse("2026-09-23T12:00:00Z"),
            EndUtc = DateTimeOffset.Parse("2026-09-23T12:00:05Z"),
            Confidence = 0.8f,
            ThumbnailPath = @"C:\secret\recordings\thumb.jpg",
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).SearchAsync(null, 1, 12, null, null, CancellationToken.None);
        result.Items.Must().HaveCount(1);
        result.Items[0].HasThumbnail.Must().BeTrue();
        typeof(EventResponse).GetProperty("ThumbnailPath").VerifyNullable().BeNull();
    }

    private static EventLibraryService CreateService(AppDbContext db) =>
        new(db, Options.Create(new StorageOptions
        {
            RecordingsDirectory = Path.Combine(Path.GetTempPath(), "nidus-tests", Guid.NewGuid().ToString("N")),
        }));

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}
