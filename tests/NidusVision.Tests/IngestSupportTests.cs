using NidusVision.Core.Ingest;
using NidusVision.Core.Models;
using NidusVision.Core.Storage;

namespace NidusVision.Tests;

public sealed class IngestSupportTests
{
    [Fact]
    public void Fingerprint_changes_when_url_or_credentials_change()
    {
        var camera = new Camera { Name = "Front", MainRtspUrl = "rtsp://cam/a", Username = "u", PasswordProtected = "p" };
        var original = CameraIngestFingerprint.From(camera);
        camera.MainRtspUrl = "rtsp://cam/b";
        CameraIngestFingerprint.From(camera).Must().NotBe(original);
        camera.MainRtspUrl = "rtsp://cam/a";
        camera.PasswordProtected = "other";
        CameraIngestFingerprint.From(camera).Must().NotBe(original);
    }

    [Fact]
    public void DiskFileSize_reads_file_length_when_indexed_size_is_stale()
    {
        var path = Path.Combine(Path.GetTempPath(), "nidus-tests", $"{Guid.NewGuid():N}.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[2048]);
        DiskFileSize.Of(path, 0).Must().Be(2048);
        File.Delete(path);
    }

    [Fact]
    public void RecordingPath_parses_strftime_filename()
    {
        RecordingPath.TryParseStart(@"C:\rec\cam\2026\09\23\20260923T183000.mp4", out var start).Must().BeTrue();
        start.Must().Be(new DateTimeOffset(2026, 9, 23, 18, 30, 0, TimeSpan.Zero));
    }
}
