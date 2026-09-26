using NidusVision.Core.Cameras;
using NidusVision.Core.Storage;

namespace NidusVision.Tests;

public sealed class StorageRootTests
{
    [Fact]
    public void ContainsAcceptsFilesInsideTheRoot()
    {
        using var directory = new TestDirectory();

        StorageRoot.Contains(directory.Path, directory.GetPath("cam", "20260923T183000.mp4")).Must().BeTrue();
    }

    [Fact]
    public void ContainsRejectsTheRootItselfSiblingsAndTraversal()
    {
        using var directory = new TestDirectory();
        var root = directory.GetPath("recordings");

        StorageRoot.Contains(root, root).Must().BeFalse();
        StorageRoot.Contains(root, root + "-other" + Path.DirectorySeparatorChar + "a.mp4").Must().BeFalse();
        StorageRoot.Contains(root, Path.Combine(root, "..", "nidus.db")).Must().BeFalse();
        StorageRoot.Contains(root, null).Must().BeFalse();
    }

    [Theory]
    [InlineData("rtsp://192.168.1.20:554/stream", true)]
    [InlineData("RTSPS://cam.local/live", true)]
    [InlineData("file:///etc/passwd", false)]
    [InlineData("http://cam.local/stream", false)]
    [InlineData("rtsp:///no-host", false)]
    [InlineData("not a url", false)]
    public void CameraUrlAcceptsOnlyRtspSchemes(string url, bool expected)
    {
        CameraUrl.IsSupported(url).Must().Be(expected);
    }
}
