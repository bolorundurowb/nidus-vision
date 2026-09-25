using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NidusVision.Data;

namespace NidusVision.Tests;

public sealed class DetectionIntervalMigrationTests
{
    [Fact]
    public async Task MigrationPreservesDetectionTimingAndNormalizesLocation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        var migrator = db.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260925143000_AddRecordingSegmentFinalized");

        var cameraId = Guid.CreateVersion7();
        var exteriorCameraId = Guid.CreateVersion7();
        var detectionId = Guid.CreateVersion7();
        var start = DateTimeOffset.Parse("2026-09-25T12:01:38Z").UtcTicks;
        var end = DateTimeOffset.Parse("2026-09-25T12:01:59Z").UtcTicks;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Cameras
                (Id, Name, Location, Enabled, MainRtspUrl, Transport, Status, CreatedAt, UpdatedAt)
            VALUES
                ({cameraId}, 'Front', 'Front yard', 1, 'rtsp://camera/stream', 0, 0, {start}, {start});
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Cameras
                (Id, Name, Location, Enabled, MainRtspUrl, Transport, Status, CreatedAt, UpdatedAt)
            VALUES
                ({exteriorCameraId}, 'Back', 'Exterior', 1, 'rtsp://camera/back', 0, 0, {start}, {start});
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO DetectionEvents
                (Id, CameraId, StartUtc, EndUtc, Confidence, BoundingBoxJson, ThumbnailPath, ClipPath, SegmentIdsJson)
            VALUES
                ({detectionId}, {cameraId}, {start}, {end}, 0.91, NULL, 'old.jpg', 'old.mp4', '[]');
            """);

        await migrator.MigrateAsync();
        db.ChangeTracker.Clear();

        var interval = await db.DetectionIntervals.AsNoTracking().SingleAsync();
        interval.Id.Must().Be(detectionId);
        interval.StartUtc.UtcTicks.Must().Be(start);
        interval.EndUtc.UtcTicks.Must().Be(end);
        var locations = await db.Cameras.AsNoTracking().ToDictionaryAsync(camera => camera.Id, camera => camera.Location);
        locations[cameraId].Must().Be(NidusVision.Core.Models.CameraLocation.Interior);
        locations[exteriorCameraId].Must().Be(NidusVision.Core.Models.CameraLocation.Exterior);
    }
}
