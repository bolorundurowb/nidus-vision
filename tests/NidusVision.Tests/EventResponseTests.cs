using Microsoft.Extensions.Options;
using NidusVision.Core.Models;
using NidusVision.Core.Options;
using NidusVision.Data;
using NidusVision.Web.Events;

namespace NidusVision.Tests;

public sealed class EventResponseTests : SqliteTestBase
{
    [Fact]
    public async Task SearchAsyncWithEventThumbnailExposesFlagWithoutFilesystemPath()
    {
        // Arrange
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

        // Act
        var result = await CreateService(db).SearchAsync(null, 1, 12, null, null, CancellationToken.None);

        // Assert
        result.Items.Must().HaveCount(1);
        result.Items[0].HasThumbnail.Must().BeTrue();
        typeof(EventResponse).GetProperty("ThumbnailPath").VerifyNullable().BeNull();
    }

    [Fact]
    public async Task SearchRecordingsAsyncWithRecordingThumbnailExposesFlagWithoutFilesystemPath()
    {
        // Arrange
        await using var db = CreateContext();
        var camera = new Camera { Name = "Front", MainRtspUrl = "rtsp://cam/stream" };
        db.Cameras.Add(camera);
        db.RecordingSegments.Add(new RecordingSegment
        {
            CameraId = camera.Id,
            Path = Path.Combine(Path.GetTempPath(), "missing.mp4"),
            StartUtc = DateTimeOffset.Parse("2026-09-23T12:00:00Z"),
            EndUtc = DateTimeOffset.Parse("2026-09-23T12:15:00Z"),
            ThumbnailPath = @"C:\secret\recordings\thumb.jpg",
        });
        await db.SaveChangesAsync();

        // Act
        var result = await CreateService(db).SearchRecordingsAsync(null, 1, 12, null, null, null, CancellationToken.None);

        // Assert
        result.Items.Must().HaveCount(1);
        result.Items[0].HasThumbnail.Must().BeTrue();
        result.Items[0].IsActive.Must().BeFalse();
        typeof(RecordingResponse).GetProperty("ThumbnailPath").VerifyNullable().BeNull();
    }

    private static EventLibraryService CreateService(AppDbContext db) =>
        new(db, Options.Create(new StorageOptions
        {
            RecordingsDirectory = Path.Combine(Path.GetTempPath(), "nidus-tests", Guid.NewGuid().ToString("N")),
        }), TimeProvider.System);

}
