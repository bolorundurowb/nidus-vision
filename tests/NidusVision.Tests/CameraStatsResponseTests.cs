using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NidusVision.Core.Models;
using NidusVision.Data;
using NidusVision.Streaming;
using NidusVision.Web.Cameras;

namespace NidusVision.Tests;

public sealed class CameraStatsResponseTests : SqliteTestBase
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-23T12:00:00Z");

    [Fact]
    public async Task ListAsyncWithRecordingMetadataReturnsCalculatedCameraStatistics()
    {
        // Arrange
        await using var db = CreateContext();
        var camera = AddCamera(db, "1920×1080", 25);
        db.RecordingSegments.AddRange(
            NewSegment(camera.Id, Now.AddDays(-3), TimeSpan.FromMinutes(30), 1_000),
            NewSegment(camera.Id, Now.AddMinutes(-40), TimeSpan.FromMinutes(30), 720_000_000));
        await db.SaveChangesAsync(CancellationToken.None);

        // Act
        var cameras = await CreateService(db).ListAsync(CancellationToken.None);
        var response = cameras[0];

        // Assert
        cameras.Must().HaveCount(1);
        response.Resolution.Must().Be("1920×1080");
        response.Fps.Must().Be(25);
        response.Bitrate.Must().Be("3.2 Mbps");
        response.Retention.Must().Be("3 days");
    }

    [Fact]
    public async Task ListAsyncWithoutRecordingsReturnsUnknownBitrateAndRetention()
    {
        // Arrange
        await using var db = CreateContext();
        AddCamera(db, resolution: null, fps: null);
        await db.SaveChangesAsync(CancellationToken.None);

        // Act
        var cameras = await CreateService(db).ListAsync(CancellationToken.None);
        var response = cameras[0];

        // Assert
        cameras.Must().HaveCount(1);
        response.Resolution.VerifyNullable().BeNull();
        response.Fps.VerifyNullable().BeNull();
        response.Bitrate.VerifyNullable().BeNull();
        response.Retention.VerifyNullable().BeNull();
    }

    [Fact]
    public async Task ListAsyncWithGrowingSegmentUsesCurrentFileSizeForBitrate()
    {
        // Arrange
        using var directory = new TestDirectory();
        var path = directory.GetPath("segment.mp4");
        await File.WriteAllBytesAsync(path, new byte[1_000_000]);
        await using var db = CreateContext();
        var camera = AddCamera(db, "1920×1080", 25);
        // Ingest indexes a segment the moment FFmpeg creates the file, so the stored size is 0.
        var segment = NewSegment(camera.Id, Now.AddMinutes(-1), TimeSpan.FromMinutes(30), 0);
        segment.Path = path;
        db.RecordingSegments.Add(segment);
        await db.SaveChangesAsync(CancellationToken.None);

        // Act
        var cameras = await CreateService(db).ListAsync(CancellationToken.None);

        // Assert
        cameras.Must().HaveCount(1);
        cameras[0].Bitrate.Must().Be("133 kbps");
    }

    private CameraService CreateService(AppDbContext db) =>
        new(db, new EphemeralDataProtectionProvider(), new RtspProbe(), new FixedTimeProvider(Now));

    private static Camera AddCamera(AppDbContext db, string? resolution, int? fps)
    {
        var camera = new Camera
        {
            Name = "Front",
            MainRtspUrl = "rtsp://localhost/front",
            LastResolution = resolution,
            LastFps = fps,
        };
        db.Cameras.Add(camera);
        return camera;
    }

    private static RecordingSegment NewSegment(Guid cameraId, DateTimeOffset start, TimeSpan duration, long byteSize) => new()
    {
        CameraId = cameraId,
        Path = $"{start:yyyyMMddHHmmss}.mp4",
        StartUtc = start,
        EndUtc = start + duration,
        ByteSize = byteSize,
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
