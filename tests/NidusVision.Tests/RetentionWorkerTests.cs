using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NidusVision.Core.Models;
using NidusVision.Data;

namespace NidusVision.Tests;

public sealed class RetentionWorkerTests
{
    [Fact]
    public async Task RetentionDeletesRecordingThumbnailFiles()
    {
        // Arrange
        using var directory = new TestDirectory();
        var recordingPath = directory.GetPath("segment.mp4");
        var thumbnailPath = directory.GetPath("segment.jpg");
        await File.WriteAllBytesAsync(recordingPath, [1, 2, 3, 4]);
        await File.WriteAllBytesAsync(thumbnailPath, [9, 8, 7]);

        var dbPath = directory.GetPath("test.db");
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={dbPath};Pooling=False"));
        await using var provider = services.BuildServiceProvider();
        await using var db = provider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.Parse("2026-09-23T00:00:00Z");
        var camera = new Camera { Name = "Front", MainRtspUrl = "rtsp://cam/stream" };
        db.Cameras.Add(camera);
        db.AppSettings.Add(new AppSettings { GeneralRetentionDays = 1, DetectionRetentionDays = 30 });
        db.RecordingSegments.Add(new RecordingSegment
        {
            CameraId = camera.Id,
            Path = recordingPath,
            StartUtc = now.AddDays(-10),
            EndUtc = now.AddDays(-10),
            ByteSize = 4,
            ThumbnailPath = thumbnailPath,
        });
        await db.SaveChangesAsync();

        // Act
        var worker = new RetentionWorker(provider.GetRequiredService<IServiceScopeFactory>(), new FixedTime(now), NullLogger<RetentionWorker>.Instance);
        await worker.RunOnceAsync(CancellationToken.None);

        // Assert
        File.Exists(recordingPath).Must().BeFalse();
        File.Exists(thumbnailPath).Must().BeFalse();
        (await db.RecordingSegments.CountAsync()).Must().Be(0);
    }

    private sealed class FixedTime(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
