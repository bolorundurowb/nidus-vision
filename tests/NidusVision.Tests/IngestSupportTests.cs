using NidusVision.Core.Ingest;
using NidusVision.Core.Models;
using NidusVision.Core.Storage;

namespace NidusVision.Tests;

public sealed class IngestSupportTests
{
    [Fact]
    public void FingerprintChangesWhenUrlOrCredentialsChange()
    {
        // Arrange
        var camera = new Camera { Name = "Front", MainRtspUrl = "rtsp://cam/a", Username = "u", PasswordProtected = "p" };
        var original = CameraIngestFingerprint.From(camera);

        // Act and assert
        camera.MainRtspUrl = "rtsp://cam/b";
        CameraIngestFingerprint.From(camera).Must().NotBe(original);
        camera.MainRtspUrl = "rtsp://cam/a";
        camera.PasswordProtected = "other";
        CameraIngestFingerprint.From(camera).Must().NotBe(original);
        camera.PasswordProtected = "p";
        camera.Name = "Back gate";
        CameraIngestFingerprint.From(camera).Must().NotBe(original);
        camera.Name = "Front";
        camera.RecordingEnabled = false;
        CameraIngestFingerprint.From(camera).Must().NotBe(original);
    }

    [Fact]
    public void FingerprintChangesWhenSegmentDurationChanges()
    {
        // Arrange
        var camera = new Camera { Name = "Front", MainRtspUrl = "rtsp://cam/a" };
        var original = CameraIngestFingerprint.From(camera, segmentDurationSeconds: 15 * 60);

        // Act and assert
        CameraIngestFingerprint.From(camera, segmentDurationSeconds: 5 * 60).Must().NotBe(original);
        CameraIngestFingerprint.From(camera, segmentDurationSeconds: 30 * 60).Must().NotBe(original);
        CameraIngestFingerprint.From(camera, segmentDurationSeconds: 15 * 60).Must().Be(original);
    }

    [Fact]
    public void DiskFileSizeReadsFileLengthWhenIndexedSizeIsStale()
    {
        // Arrange
        using var directory = new TestDirectory();
        var path = directory.GetPath("segment.bin");
        File.WriteAllBytes(path, new byte[2048]);

        // Act
        var size = DiskFileSize.Of(path, indexedBytes: 0);

        // Assert
        size.Must().Be(2048);
    }

    [Fact]
    public void RecordingPathParsesStrftimeFilename()
    {
        // Arrange
        var path = Path.Combine("rec", "cam", "2026", "09", "23", "20260923T183000.mp4");

        // Act
        var parsed = RecordingPath.TryParseStart(path, out var start);

        // Assert
        parsed.Must().BeTrue();
        start.Must().Be(new DateTimeOffset(2026, 9, 23, 18, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void RecordingPathRejectsFilenamesThatAreNotTimestamps()
    {
        // Act
        var parsed = RecordingPath.TryParseStart(Path.Combine("rec", "cam", "segment.mp4"), out _);

        // Assert
        parsed.Must().BeFalse();
    }

    [Fact]
    public void RecordingPathBuildsAReadableCameraFolderName()
    {
        var id = Guid.Parse("0123456789abcdef0123456789abcdef");

        RecordingPath.Slug("Front Door").Must().Be("front-door");
        RecordingPath.Slug("  Café / Cam!! ").Must().Be("café-cam");
        RecordingPath.Slug("!!!").Must().Be("");
        RecordingPath.Slug(new string('A', 80)).Length.Must().Be(RecordingPath.MaxSlugLength);
        RecordingPath.CameraDirectoryName(id, "Front Door").Must().Be(id.ToString("N") + "-front-door");
        RecordingPath.CameraDirectoryName(id, "!!!").Must().Be(id.ToString("N"));
        RecordingPath.TryParseCameraId(id.ToString("N"), out var bare).Must().BeTrue();
        bare.Must().Be(id);
        RecordingPath.TryParseCameraId(id.ToString("N") + "-front-door", out var named).Must().BeTrue();
        named.Must().Be(id);
        RecordingPath.TryParseCameraId(id.ToString("N") + "front", out _).Must().BeFalse();
        RecordingPath.TryParseCameraId("not-a-camera", out _).Must().BeFalse();
    }
}
