using Microsoft.Extensions.Options;
using NidusVision.Core.Models;
using NidusVision.Core.Options;
using NidusVision.Data;
using NidusVision.Web.Events;

namespace NidusVision.Tests;

public sealed class EventResponseTests : SqliteTestBase
{
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
        var result = await CreateService(db).SearchRecordingsAsync(null, null, 1, 12, null, null, null, CancellationToken.None);

        // Assert
        result.Items.Must().HaveCount(1);
        result.Items[0].HasThumbnail.Must().BeTrue();
        result.Items[0].IsActive.Must().BeFalse();
        typeof(RecordingResponse).GetProperty("ThumbnailPath").VerifyNullable().BeNull();
    }

    private static RecordingLibraryService CreateService(AppDbContext db) =>
        new(db, Options.Create(new StorageOptions
        {
            RecordingsDirectory = Path.Combine(Path.GetTempPath(), "nidus-tests", Guid.NewGuid().ToString("N")),
        }), TimeProvider.System);

}
