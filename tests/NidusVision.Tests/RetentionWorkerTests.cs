using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NidusVision.Core.Models;
using NidusVision.Core.Options;
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
        await CreateWorker(provider, now, directory.Path).RunOnceAsync(CancellationToken.None);

        // Assert
        File.Exists(recordingPath).Must().BeFalse();
        File.Exists(thumbnailPath).Must().BeFalse();
        (await db.RecordingSegments.CountAsync()).Must().Be(0);
    }

    [Fact]
    public async Task RetentionLeavesFilesOutsideTheRecordingsDirectory()
    {
        // Arrange
        using var directory = new TestDirectory();
        var recordings = directory.GetPath("recordings");
        Directory.CreateDirectory(recordings);
        var outsidePath = directory.GetPath("outside.mp4");
        await File.WriteAllBytesAsync(outsidePath, [1, 2, 3, 4]);

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
            Path = outsidePath,
            StartUtc = now.AddDays(-10),
            EndUtc = now.AddDays(-10),
            ByteSize = 4,
        });
        await db.SaveChangesAsync();

        // Act
        await CreateWorker(provider, now, recordings).RunOnceAsync(CancellationToken.None);

        // Assert
        File.Exists(outsidePath).Must().BeTrue();
        (await db.RecordingSegments.CountAsync()).Must().Be(0);
    }

    private static RetentionWorker CreateWorker(ServiceProvider provider, DateTimeOffset now, string recordingsDirectory) =>
        new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FixedTime(now),
            Options.Create(new StorageOptions { RecordingsDirectory = recordingsDirectory }),
            NullLogger<RetentionWorker>.Instance);

    private sealed class FixedTime(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
