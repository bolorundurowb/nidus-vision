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
    public void SegmentDuration_DefaultsToThirtyMinutes()
    {
        var options = new StorageOptions();
        var startInfo = new FfmpegSegmentProcess().CreateStartInfo(
            "rtsp://camera/stream",
            "tcp",
            CreateTempDirectory(),
            options.EffectiveSegmentDurationSeconds);

        AssertArgumentValue(startInfo.ArgumentList, "-segment_time", "1800");
        AssertArgumentValue(startInfo.ArgumentList, "-break_non_keyframes", "1");
    }

    [Fact]
    public void SegmentDuration_UnderThirtyMinutesIsNotChanged()
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

        Assert.Equal(1800, options.EffectiveSegmentDurationSeconds);
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
            EndUtc = start.AddMinutes(30),
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
        Assert.NotNull(detection.ClipPath);
        Assert.NotEqual(Path.GetFullPath(recordingPath), Path.GetFullPath(detection.ClipPath!));
        Assert.StartsWith(
            Path.GetFullPath(events),
            Path.GetFullPath(detection.ClipPath!),
            StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(detection.ClipPath));
        Assert.Equal([1, 2, 3, 4], await File.ReadAllBytesAsync(detection.ClipPath!));
    }

    private static void AssertArgumentValue(
        System.Collections.ObjectModel.Collection<string> arguments,
        string argument,
        string expected)
    {
        var index = arguments.IndexOf(argument);
        Assert.True(index >= 0);
        Assert.Equal(expected, arguments[index + 1]);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "nidus-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
