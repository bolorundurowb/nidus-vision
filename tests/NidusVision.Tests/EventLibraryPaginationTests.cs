using Microsoft.Extensions.Options;
using NidusVision.Core.Models;
using NidusVision.Core.Options;
using NidusVision.Data;
using NidusVision.Web.Events;

namespace NidusVision.Tests;

public sealed class EventLibraryPaginationTests : SqliteTestBase
{
    [Fact]
    public async Task SearchRecordingsAsyncWithCameraAndPageReturnsRequestedRecordings()
    {
        // Arrange
        await using var db = CreateContext();
        var front = NewCamera("Front Yard");
        var garage = NewCamera("Garage");
        db.Cameras.AddRange(front, garage);
        var start = DateTimeOffset.Parse("2026-09-23T12:00:00Z");
        var frontRecordings = Enumerable.Range(1, 5)
            .Select(index => NewRecording(front.Id, start.AddMinutes(index), index))
            .ToArray();
        db.RecordingSegments.AddRange(frontRecordings);
        db.RecordingSegments.Add(NewRecording(garage.Id, start.AddMinutes(6), 6));
        await db.SaveChangesAsync();

        // Act
        var result = await CreateService(db).SearchRecordingsAsync(
            front.Id,
            location: null,
            page: 2,
            pageSize: 2,
            fromUtc: null,
            toUtc: null,
            hasHuman: null,
            CancellationToken.None);

        // Assert
        result.TotalCount.Must().Be(5);
        result.TotalPages.Must().Be(3);
        result.Items.Select(item => item.Id).Must().BeSequenceEqual(
            [frontRecordings[2].Id, frontRecordings[1].Id]);
    }

    [Fact]
    public async Task SearchRecordingsAsyncWithDateRangeFiltersRecordings()
    {
        // Arrange
        await using var db = CreateContext();
        var camera = NewCamera("Front Yard");
        db.Cameras.Add(camera);
        var day = DateTimeOffset.Parse("2026-09-23T00:00:00Z");
        db.RecordingSegments.AddRange(
            NewRecording(camera.Id, day.AddMinutes(-10), 1),
            NewRecording(camera.Id, day.AddHours(10), 2),
            NewRecording(camera.Id, day.AddDays(1), 3));
        await db.SaveChangesAsync();

        // Act
        var recordings = await CreateService(db).SearchRecordingsAsync(
            camera.Id,
            location: null,
            page: 1,
            pageSize: 12,
            fromUtc: day,
            toUtc: day.AddDays(1),
            hasHuman: null,
            CancellationToken.None);

        // Assert
        recordings.TotalCount.Must().Be(2);
        recordings.Items.Select(item => item.ByteSize).Must().BeSequenceEqual([2L, 1L]);
    }

    [Fact]
    public async Task SearchRecordingsAsyncFiltersByHasHuman()
    {
        await using var db = CreateContext();
        var camera = NewCamera("Front Yard");
        db.Cameras.Add(camera);
        var start = DateTimeOffset.Parse("2026-09-23T12:00:00Z");
        db.RecordingSegments.AddRange(
            NewRecording(camera.Id, start, 1),
            NewRecording(camera.Id, start.AddMinutes(15), 2));
        var withHuman = NewRecording(camera.Id, start.AddMinutes(30), 3);
        withHuman.HasHuman = true;
        db.RecordingSegments.Add(withHuman);
        await db.SaveChangesAsync();

        var withDetections = await CreateService(db).SearchRecordingsAsync(
            camera.Id, null, 1, 12, null, null, true, CancellationToken.None);
        var without = await CreateService(db).SearchRecordingsAsync(
            camera.Id, null, 1, 12, null, null, false, CancellationToken.None);

        withDetections.TotalCount.Must().Be(1);
        withDetections.Items[0].HasHuman.Must().BeTrue();
        without.TotalCount.Must().Be(2);
        without.Items.All(item => !item.HasHuman).Must().BeTrue();
    }

    [Fact]
    public async Task SearchRecordingsAsyncReturnsClippedDetectionIntervals()
    {
        await using var db = CreateContext();
        var camera = NewCamera("Exterior");
        camera.Location = CameraLocation.Exterior;
        var start = DateTimeOffset.Parse("2026-09-23T12:00:00Z");
        db.Cameras.Add(camera);
        db.RecordingSegments.Add(NewRecording(camera.Id, start, 1));
        db.DetectionIntervals.Add(new DetectionInterval
        {
            CameraId = camera.Id,
            StartUtc = start.AddMinutes(-1),
            EndUtc = start.AddMinutes(2),
            Confidence = 0.9f,
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).SearchRecordingsAsync(
            null, CameraLocation.Exterior, 1, 12, null, null, null, CancellationToken.None);

        result.Items.Must().HaveCount(1);
        result.Items[0].DetectionIntervals.Must().HaveCount(1);
        result.Items[0].DetectionIntervals[0].StartUtc.Must().Be(start);
        result.Items[0].Location.Must().Be("Exterior");
    }

    private RecordingLibraryService CreateService(AppDbContext db) =>
        new(db, Options.Create(new StorageOptions
        {
            RecordingsDirectory = Path.Combine(Path.GetTempPath(), "nidus-tests", Guid.NewGuid().ToString("N")),
        }), TimeProvider.System);

    private static Camera NewCamera(string name) =>
        new() { Name = name, MainRtspUrl = $"rtsp://localhost/{name}" };

    private static RecordingSegment NewRecording(Guid cameraId, DateTimeOffset start, int index) =>
        new()
        {
            CameraId = cameraId,
            Path = Path.Combine(Path.GetTempPath(), $"missing-{index}.mp4"),
            StartUtc = start,
            EndUtc = start.AddMinutes(15),
            ByteSize = index,
        };
}
