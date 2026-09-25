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
    public async Task GrowingFragmentedSourceIsReadyImmediately()
    {
        // Arrange
        using var directory = new TestDirectory("nidus-thumbs");
        var path = directory.GetPath("recording.mp4");
        await File.WriteAllBytesAsync(path, [1, 2, 3, 4]);
        var ready = VideoThumbnailExtractor.IsSourceReady(path);

        // Assert
        ready.Must().BeTrue();
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
        var missing = new RecordingSegment { Path = "missing.mp4", ThumbnailPath = null };
        var present = new RecordingSegment { Path = "present.mp4", ThumbnailPath = path };
        var extra = new RecordingSegment { Path = "extra.mp4", ThumbnailPath = "" };

        // Act
        var selected = ThumbnailWorker.SelectMissing([present, missing, extra], e => e.ThumbnailPath, limit: 1);

        // Assert
        selected.Must().HaveCount(1);
        selected[0].Must().Be(missing);
    }

}
