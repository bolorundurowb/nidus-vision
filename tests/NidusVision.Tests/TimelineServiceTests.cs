using NidusVision.Core.Models;
using NidusVision.Data;
using NidusVision.Web.Timeline;

namespace NidusVision.Tests;

public sealed class TimelineServiceTests : SqliteTestBase
{
    [Fact]
    public async Task GetAsyncWithOverlappingSegmentsAndDetectionOverlaysHumanInterval()
    {
        // Arrange
        await using var db = CreateContext();
        var camera = new Camera { Name = "Front", MainRtspUrl = "rtsp://cam/stream" };
        var start = DateTimeOffset.Parse("2026-09-23T14:00:00Z");
        db.Cameras.Add(camera);
        db.RecordingSegments.Add(new RecordingSegment
        {
            CameraId = camera.Id,
            Path = "a.mp4",
            StartUtc = start,
            EndUtc = start.AddMinutes(20),
            HasHuman = true,
            ByteSize = 10,
        });
        db.RecordingSegments.Add(new RecordingSegment
        {
            CameraId = camera.Id,
            Path = "b.mp4",
            StartUtc = start.AddMinutes(15),
            EndUtc = start.AddMinutes(30),
            ByteSize = 10,
        });
        db.DetectionEvents.Add(new DetectionEvent
        {
            CameraId = camera.Id,
            StartUtc = start.AddMinutes(5),
            EndUtc = start.AddMinutes(6),
            Confidence = 0.9f,
        });
        await db.SaveChangesAsync();

        // Act
        var rows = await new TimelineService(db).GetAsync(start, start.AddHours(1), CancellationToken.None);

        // Assert
        rows.Must().HaveCount(1);
        var continuous = rows[0].Intervals.Single(i => !i.Human);
        continuous.Start.Must().Be(start);
        continuous.End.Must().Be(start.AddMinutes(30));
        var human = rows[0].Intervals.Single(i => i.Human);
        human.Start.Must().Be(start.AddMinutes(5));
        human.End.Must().Be(start.AddMinutes(6));
    }
}
