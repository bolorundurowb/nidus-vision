using System.Diagnostics;
using NidusVision.Core.Models;
using NidusVision.Web.Events;

namespace NidusVision.Tests;

public sealed class VideoThumbnailExtractorTests
{
    [Fact]
    public void RecordingThumbnailIsAJpgSiblingOfTheMp4()
    {
        var path = Path.Combine("recordings", "cam", "20260923T183000.mp4");
        VideoThumbnailExtractor.DestinationForRecording(path).Must().Be(Path.Combine("recordings", "cam", "20260923T183000.jpg"));
    }

    [Fact]
    public void EventThumbnailLivesBesideTheClipUnderTheEventsDirectory()
    {
        // Arrange
        using var directory = new TestDirectory("nidus-thumbs");
        var cameraId = Guid.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var eventId = Guid.Parse("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        var start = new DateTimeOffset(2026, 9, 23, 18, 30, 0, TimeSpan.Zero);

        // Act
        var path = VideoThumbnailExtractor.DestinationForEvent(directory.Path, cameraId, start, eventId);

        // Assert
        path.Must().Be(Path.Combine(directory.Path, cameraId.ToString("N"), "2026", "09", "23", $"{eventId:N}.jpg"));
    }

    [Fact]
    public void NeedsThumbnailWhenPathIsMissingOrFileIsGone()
    {
        VideoThumbnailExtractor.NeedsThumbnail(null).Must().BeTrue();
        VideoThumbnailExtractor.NeedsThumbnail("").Must().BeTrue();
        VideoThumbnailExtractor.NeedsThumbnail(Path.Combine(Path.GetTempPath(), "missing-thumb.jpg")).Must().BeTrue();
    }

    [Fact]
    public async Task NeedsThumbnailIsFalseWhenTheJpegExists()
    {
        // Arrange
        using var directory = new TestDirectory("nidus-thumbs");
        var path = directory.GetPath("thumbnail.jpg");
        await File.WriteAllBytesAsync(path, [1, 2, 3]);

        // Act
        var needsThumbnail = VideoThumbnailExtractor.NeedsThumbnail(path);

        // Assert
        needsThumbnail.Must().BeFalse();
    }

    [Fact]
    public async Task SourceIsNotReadyUntilTheFileStopsGrowing()
    {
        // Arrange
        using var directory = new TestDirectory("nidus-thumbs");
        var path = directory.GetPath("recording.mp4");
        await File.WriteAllBytesAsync(path, [1, 2, 3, 4]);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        var later = new FixedTime(DateTimeOffset.UtcNow.AddSeconds(11));

        // Act
        var readyImmediately = VideoThumbnailExtractor.IsSourceReady(path, TimeProvider.System);
        var readyLater = VideoThumbnailExtractor.IsSourceReady(path, later);

        // Assert
        readyImmediately.Must().BeFalse();
        readyLater.Must().BeTrue();
    }

    [Fact]
    public void EventClipSeeksToTheDetectionMoment()
    {
        var detection = new DetectionEvent
        {
            Id = Guid.Parse("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"),
            StartUtc = DateTimeOffset.Parse("2026-09-23T12:00:15Z"),
            EndUtc = DateTimeOffset.Parse("2026-09-23T12:00:20Z"),
        };
        var clip = Path.Combine("events", $"{detection.Id:N}.mp4");

        VideoThumbnailExtractor.SeekForEvent(clip, detection, null).Must().Be(TimeSpan.FromSeconds(15));
        VideoThumbnailExtractor.SeekForEvent(
            "segment.mp4",
            detection,
            DateTimeOffset.Parse("2026-09-23T12:00:00Z")).Must().Be(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void ExtractArgumentsSeekThenScaleASingleJpegFrame()
    {
        var startInfo = new ProcessStartInfo();
        VideoThumbnailExtractor.ConfigureExtract(startInfo, "in.mp4", "out.jpg", TimeSpan.FromSeconds(2));

        startInfo.ArgumentList.Must().BeSequenceEqual([
            "-hide_banner", "-loglevel", "error",
            "-ss", "00:00:02",
            "-i", "in.mp4",
            "-frames:v", "1",
            "-vf", "scale=640:-2",
            "-q:v", "4",
            "-f", "mjpeg",
            "-y", "out.jpg",
        ]);
    }

    [Fact]
    public async Task WorkerSelectsRowsWithoutThumbnailsAndSkipsTheRest()
    {
        // Arrange
        using var directory = new TestDirectory("nidus-thumbs");
        var path = directory.GetPath("thumbnail.jpg");
        await File.WriteAllBytesAsync(path, [1]);
        var missing = new DetectionEvent { ThumbnailPath = null };
        var present = new DetectionEvent { ThumbnailPath = path };
        var extra = new DetectionEvent { ThumbnailPath = "" };

        // Act
        var selected = ThumbnailWorker.SelectMissing([present, missing, extra], e => e.ThumbnailPath, limit: 1);

        // Assert
        selected.Must().HaveCount(1);
        selected[0].Must().Be(missing);
    }

    private sealed class FixedTime(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
