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
}
