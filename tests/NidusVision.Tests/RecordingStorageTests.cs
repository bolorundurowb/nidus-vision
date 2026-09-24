using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NidusVision.Core.Models;
using NidusVision.Core.Options;
using NidusVision.Data;
using NidusVision.Streaming;
using NidusVision.Web.Events;
using NidusVision.Web.Inference;

namespace NidusVision.Tests;

public sealed class RecordingStorageTests
{
    [Fact]
    public void SegmentDuration_DefaultsToFifteenMinutes()
    {
        var options = new StorageOptions();
        var startInfo = new FfmpegSegmentProcess().CreateStartInfo(
            "rtsp://camera/stream",
            "tcp",
            CreateTempDirectory(),
            options.EffectiveSegmentDurationSeconds);

        options.EffectiveSegmentDurationSeconds.Must().Be(900);
        AssertArgumentValue(startInfo.ArgumentList, "-segment_time", "900");
        AssertArgumentValue(startInfo.ArgumentList, "-break_non_keyframes", "1");
        AssertArgumentValue(startInfo.ArgumentList, "-strftime_mkdir", "1");
        startInfo.ArgumentList[^1].Must().Contain("%Y");
        startInfo.ArgumentList.Must().NotContain("10");
    }

    [Fact]
    public void SegmentDuration_ConfiguredShorterIsNotChanged()
    {
        var options = new StorageOptions { SegmentDurationSeconds = 5 * 60 };
        var startInfo = new FfmpegSegmentProcess().CreateStartInfo(
            "rtsp://camera/stream",
            "tcp",
            CreateTempDirectory(),
            options.EffectiveSegmentDurationSeconds);

        AssertArgumentValue(startInfo.ArgumentList, "-segment_time", "300");
    }

    [Fact]
    public void SegmentDuration_ConfiguredLongerWithinCapIsNotChanged()
    {
        var options = new StorageOptions { SegmentDurationSeconds = 17 * 60 };
        var startInfo = new FfmpegSegmentProcess().CreateStartInfo(
            "rtsp://camera/stream",
            "tcp",
            CreateTempDirectory(),
            options.EffectiveSegmentDurationSeconds);

        AssertArgumentValue(startInfo.ArgumentList, "-segment_time", "1020");
    }

    [Fact]
    public void SegmentDuration_OverThirtyMinutesIsCapped()
    {
        var options = new StorageOptions { SegmentDurationSeconds = 60 * 60 };

        options.EffectiveSegmentDurationSeconds.Must().Be(1800);
    }

    [Fact]
    public async Task DetectionClip_IsStoredAndIndexedOutsideRecordings()
    {
        var root = CreateTempDirectory();
        var recordings = Path.Combine(root, "recordings");
        var events = Path.Combine(root, "events");
        Directory.CreateDirectory(recordings);
        var recordingPath = Path.Combine(recordings, "segment.mp4");
        await File.WriteAllBytesAsync(recordingPath, [1, 2, 3, 4]);

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(root, "test.db")}")
            .Options;
        await using var db = new AppDbContext(dbOptions);
        await db.Database.EnsureCreatedAsync();

        var camera = new Camera { Name = "Front", MainRtspUrl = "rtsp://camera/stream" };
        var start = DateTimeOffset.UtcNow;
        db.Cameras.Add(camera);
        db.RecordingSegments.Add(new RecordingSegment
        {
            CameraId = camera.Id,
            Path = recordingPath,
            StartUtc = start,
            EndUtc = start.AddMinutes(15),
            ByteSize = 4,
        });
        await db.SaveChangesAsync();

        var store = new EventArtifactStore(Options.Create(new StorageOptions
        {
            RecordingsDirectory = recordings,
            EventsDirectory = events,
        }));
        await DetectionHostedService.PersistDetectionWithArtifactsAsync(
            db,
            store,
            camera.Id,
            0.95f,
            start.AddMinutes(1),
            start.AddMinutes(1).AddSeconds(5),
            CancellationToken.None);

        var detection = await db.DetectionEvents.SingleAsync();
        detection.ClipPath.Must().NotBeNull();
        Path.GetFullPath(detection.ClipPath!).Must().Be(Path.GetFullPath(recordingPath));
    }

    [Fact]
    public async Task RecordingLibrary_IndexesFilesMissedByTheIngestWatcher()
    {
        var root = CreateTempDirectory();
        var recordings = Path.Combine(root, "recordings");
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(root, "test.db")}")
            .Options;
        await using var db = new AppDbContext(dbOptions);
        await db.Database.EnsureCreatedAsync();

        var camera = new Camera { Name = "Front", MainRtspUrl = "rtsp://camera/stream" };
        db.Cameras.Add(camera);
        await db.SaveChangesAsync();

        var directory = Path.Combine(recordings, camera.Id.ToString("N"), "2026", "09", "23");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "20260923T183000.mp4");
        await File.WriteAllBytesAsync(path, [1, 2, 3, 4]);

        var library = new EventLibraryService(db, Options.Create(new StorageOptions
        {
            RecordingsDirectory = recordings,
        }));
        var result = await library.SearchRecordingsAsync(null, null, 1, 12, CancellationToken.None);

        result.Items.Must().HaveCount(1);
        result.TotalCount.Must().Be(1);
        var recording = result.Items[0];
        recording.CameraId.Must().Be(camera.Id);
        recording.StartUtc.Must().Be(new DateTimeOffset(2026, 9, 23, 18, 30, 0, TimeSpan.Zero));
        recording.ByteSize.Must().Be(4);
        recording.Available.Must().BeTrue();
        (await library.ResolveRecordingPathAsync(recording.Id, CancellationToken.None)).Must().Be(path);
    }

    [Fact]
    public async Task RecordingLibrary_ReturnsEmptyWhenStorageDoesNotExist()
    {
        var root = CreateTempDirectory();
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(root, "test.db")}")
            .Options;
        await using var db = new AppDbContext(dbOptions);
        await db.Database.EnsureCreatedAsync();

        var library = new EventLibraryService(db, Options.Create(new StorageOptions
        {
            RecordingsDirectory = Path.Combine(root, "missing"),
        }));

        var result = await library.SearchRecordingsAsync(null, null, 1, 12, CancellationToken.None);
        result.Items.Must().BeEmpty();
        result.TotalCount.Must().Be(0);
    }

    private static void AssertArgumentValue(
        System.Collections.ObjectModel.Collection<string> arguments,
        string argument,
        string expected)
    {
        var index = arguments.IndexOf(argument);
        index.Must().BeGreaterThanOrEqualTo(0);
        arguments[index + 1].Must().Be(expected);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "nidus-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
