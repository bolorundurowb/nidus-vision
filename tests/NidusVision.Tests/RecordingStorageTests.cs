using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NidusVision.Core.Models;
using NidusVision.Core.Options;
using NidusVision.Data;
using NidusVision.Streaming;
using NidusVision.Web.Events;
using NidusVision.Web.Ingest;
using NidusVision.Web.Inference;

namespace NidusVision.Tests;

public sealed class RecordingStorageTests : IDisposable
{
    private readonly TestDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void SegmentDurationDefaultsToFifteenMinutes()
    {
        var options = new StorageOptions();
        var startInfo = new FfmpegSegmentProcess().CreateStartInfo(
            "rtsp://camera/stream",
            "tcp",
            _directory.Path,
            options.EffectiveSegmentDurationSeconds);

        options.EffectiveSegmentDurationSeconds.Must().Be(900);
        AssertArgumentValue(startInfo.ArgumentList, "-segment_time", "900");
        AssertArgumentValue(
            startInfo.ArgumentList,
            "-segment_format_options",
            "movflags=+frag_keyframe+empty_moov+default_base_moof");
        AssertArgumentValue(startInfo.ArgumentList, "-strftime", "1");
        AssertArgumentValue(startInfo.ArgumentList, "-timeout", "10000000");
        startInfo.ArgumentList.Must().NotContain("-break_non_keyframes");
        startInfo.ArgumentList[^1].Must().Be(Path.Combine(_directory.Path, "%Y%m%dT%H%M%S.mp4"));
        startInfo.ArgumentList.Must().NotContain("10");
        startInfo.Environment["TZ"].Must().Be("UTC");
    }

    [Fact]
    public void DetectionSampleAddsRawRgbPipeWithoutChangingSegmentPath()
    {
        var startInfo = new FfmpegSegmentProcess().CreateStartInfo(
            "rtsp://camera/stream",
            "tcp",
            _directory.Path,
            900,
            detectionSampleFps: 1);

        startInfo.ArgumentList.Must().Contain("rawvideo");
        startInfo.ArgumentList.Must().Contain("rgb24");
        startInfo.ArgumentList.Must().Contain("pipe:1");
        startInfo.ArgumentList.Must().Contain(Path.Combine(_directory.Path, "%Y%m%dT%H%M%S.mp4"));
        startInfo.ArgumentList.Any(argument => argument.Contains("%Y/%m/%d", StringComparison.Ordinal)).Must().BeFalse();
    }

    [Fact]
    public void SegmentDurationConfiguredShorterIsNotChanged()
    {
        var options = new StorageOptions { SegmentDurationSeconds = 5 * 60 };
        var startInfo = new FfmpegSegmentProcess().CreateStartInfo(
            "rtsp://camera/stream",
            "tcp",
            _directory.Path,
            options.EffectiveSegmentDurationSeconds);

        AssertArgumentValue(startInfo.ArgumentList, "-segment_time", "300");
    }

    [Fact]
    public void SegmentDurationConfiguredLongerWithinCapIsNotChanged()
    {
        var options = new StorageOptions { SegmentDurationSeconds = 17 * 60 };
        var startInfo = new FfmpegSegmentProcess().CreateStartInfo(
            "rtsp://camera/stream",
            "tcp",
            _directory.Path,
            options.EffectiveSegmentDurationSeconds);

        AssertArgumentValue(startInfo.ArgumentList, "-segment_time", "1020");
    }

    [Fact]
    public void SegmentDurationOverThirtyMinutesIsCapped()
    {
        var options = new StorageOptions { SegmentDurationSeconds = 60 * 60 };

        options.EffectiveSegmentDurationSeconds.Must().Be(1800);
    }

    [Fact]
    public async Task DetectionIntervalMarksOverlappingRecordingWithoutCreatingAClip()
    {
        // Arrange
        var root = _directory.Path;
        var recordings = Path.Combine(root, "recordings");
        Directory.CreateDirectory(recordings);
        var recordingPath = Path.Combine(recordings, "segment.mp4");
        await File.WriteAllBytesAsync(recordingPath, [1, 2, 3, 4]);

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(root, "test.db")};Pooling=False")
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

        // Act
        var detection = await DetectionHostedService.PersistDetectionIntervalAsync(
            db,
            camera.Id,
            0.95f,
            start.AddMinutes(1),
            start.AddMinutes(1).AddSeconds(5),
            CancellationToken.None);

        // Assert
        (await db.RecordingSegments.SingleAsync()).HasHuman.Must().BeTrue();
    }

    [Fact]
    public async Task RecordingLibraryIndexesFilesMissedByTheIngestWatcher()
    {
        // Arrange
        var root = _directory.Path;
        var recordings = Path.Combine(root, "recordings");
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(root, "test.db")};Pooling=False")
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

        var library = new RecordingLibraryService(db, Options.Create(new StorageOptions
        {
            RecordingsDirectory = recordings,
        }), TimeProvider.System);
        // Act
        var result = await library.SearchRecordingsAsync(null, null, 1, 12, null, null, null, CancellationToken.None);

        // Assert
        result.Items.Must().HaveCount(1);
        result.TotalCount.Must().Be(1);
        var recording = result.Items[0];
        recording.CameraId.Must().Be(camera.Id);
        recording.StartUtc.Must().Be(new DateTimeOffset(2026, 9, 23, 18, 30, 0, TimeSpan.Zero));
        recording.ByteSize.Must().Be(4);
        recording.Available.Must().BeTrue();
        recording.IsActive.Must().BeFalse();
        (await library.ResolveRecordingPathAsync(recording.Id, CancellationToken.None)).Must().Be(path);
    }

    [Fact]
    public async Task RecordingLibraryReturnsEmptyWhenStorageDoesNotExist()
    {
        // Arrange
        var root = _directory.Path;
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(root, "test.db")};Pooling=False")
            .Options;
        await using var db = new AppDbContext(dbOptions);
        await db.Database.EnsureCreatedAsync();

        var library = new RecordingLibraryService(db, Options.Create(new StorageOptions
        {
            RecordingsDirectory = Path.Combine(root, "missing"),
        }), TimeProvider.System);

        // Act
        var result = await library.SearchRecordingsAsync(null, null, 1, 12, null, null, null, CancellationToken.None);

        // Assert
        result.Items.Must().BeEmpty();
        result.TotalCount.Must().Be(0);
    }

    [Fact]
    public async Task RecordingLibraryReportsLiveSizeForOpenSegments()
    {
        var root = _directory.Path;
        var recordings = Path.Combine(root, "live");
        Directory.CreateDirectory(recordings);
        var path = Path.Combine(recordings, "open.mp4");
        await File.WriteAllBytesAsync(path, new byte[4096]);

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(root, "live.db")};Pooling=False")
            .Options;
        await using var db = new AppDbContext(dbOptions);
        await db.Database.EnsureCreatedAsync();
        var camera = new Camera { Name = "Front", MainRtspUrl = "rtsp://camera/stream" };
        var start = DateTimeOffset.UtcNow.AddMinutes(-2);
        db.Cameras.Add(camera);
        db.RecordingSegments.Add(new RecordingSegment
        {
            CameraId = camera.Id,
            Path = path,
            StartUtc = start,
            EndUtc = start.AddMinutes(15),
            ByteSize = 0,
            IsFinalized = false,
        });
        await db.SaveChangesAsync();

        var library = new RecordingLibraryService(db, Options.Create(new StorageOptions
        {
            RecordingsDirectory = recordings,
        }), TimeProvider.System);
        var result = await library.SearchRecordingsAsync(null, null, 1, 12, null, null, null, CancellationToken.None);

        result.Items.Must().HaveCount(1);
        result.Items[0].IsActive.Must().BeTrue();
        result.Items[0].ByteSize.Must().Be(4096);
        result.Items[0].EndUtc.Must().NotBe(start.AddMinutes(15));
    }

    [Fact]
    public async Task SegmentIndexedAfterDetectionIsMarkedHasHuman()
    {
        var root = _directory.Path;
        var recordings = Path.Combine(root, "late");
        Directory.CreateDirectory(recordings);
        var path = Path.Combine(recordings, "segment.mp4");
        await File.WriteAllBytesAsync(path, [1, 2, 3, 4]);

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(root, "late.db")};Pooling=False")
            .Options;
        await using var db = new AppDbContext(dbOptions);
        await db.Database.EnsureCreatedAsync();
        var camera = new Camera { Name = "Front", MainRtspUrl = "rtsp://camera/stream" };
        var start = DateTimeOffset.UtcNow;
        db.Cameras.Add(camera);
        await db.SaveChangesAsync();

        await DetectionHostedService.PersistDetectionIntervalAsync(
            db,
            camera.Id,
            0.9f,
            start.AddSeconds(5),
            start.AddSeconds(10),
            CancellationToken.None);

        var segment = new RecordingSegment
        {
            CameraId = camera.Id,
            Path = path,
            StartUtc = start,
            EndUtc = start.AddMinutes(15),
            IsFinalized = false,
        };
        db.RecordingSegments.Add(segment);
        await CameraIngestHostedService.LinkOverlappingDetectionsAsync(db, segment, CancellationToken.None);
        await db.SaveChangesAsync();

        (await db.RecordingSegments.SingleAsync()).HasHuman.Must().BeTrue();
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

}
