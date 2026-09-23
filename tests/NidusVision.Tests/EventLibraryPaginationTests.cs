using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NidusVision.Core.Models;
using NidusVision.Core.Options;
using NidusVision.Data;
using NidusVision.Web.Events;

namespace NidusVision.Tests;

public sealed class EventLibraryPaginationTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public EventLibraryPaginationTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Events_are_filtered_counted_and_paginated_in_newest_first_order()
    {
        await using var db = CreateContext();
        var front = NewCamera("Front Yard");
        var garage = NewCamera("Garage");
        db.Cameras.AddRange(front, garage);
        var start = DateTimeOffset.Parse("2026-09-23T12:00:00Z");
        db.DetectionEvents.AddRange(
            NewEvent(front.Id, start.AddMinutes(1), 0.6f),
            NewEvent(front.Id, start.AddMinutes(2), 0.7f),
            NewEvent(front.Id, start.AddMinutes(3), 0.8f),
            NewEvent(front.Id, start.AddMinutes(4), 0.9f),
            NewEvent(garage.Id, start.AddMinutes(5), 0.95f));
        await db.SaveChangesAsync();

        var result = await CreateService(db).SearchAsync(
            "front",
            front.Id,
            0.7f,
            page: 2,
            pageSize: 2,
            CancellationToken.None);

        result.TotalCount.Must().Be(3);
        result.TotalPages.Must().Be(2);
        result.Page.Must().Be(2);
        result.Items.Must().HaveCount(1);
        result.Items[0].Confidence.Must().Be(0.7f);
    }

    [Fact]
    public async Task Recordings_return_only_the_requested_page_and_camera()
    {
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

        var result = await CreateService(db).SearchRecordingsAsync(
            "front",
            front.Id,
            page: 2,
            pageSize: 2,
            CancellationToken.None);

        result.TotalCount.Must().Be(5);
        result.TotalPages.Must().Be(3);
        result.Items.Select(item => item.Id).Must().BeSequenceEqual(
            [frontRecordings[2].Id, frontRecordings[1].Id]);
    }

    private EventLibraryService CreateService(AppDbContext db) =>
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

    private static Camera NewCamera(string name) =>
        new() { Name = name, MainRtspUrl = $"rtsp://localhost/{name}" };

    private static DetectionEvent NewEvent(Guid cameraId, DateTimeOffset start, float confidence) =>
        new()
        {
            CameraId = cameraId,
            StartUtc = start,
            EndUtc = start.AddSeconds(5),
            Confidence = confidence,
        };

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
