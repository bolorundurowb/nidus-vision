using NidusVision.Core.Security;

namespace NidusVision.Tests;

public sealed class RtspUrlCredentialsTests
{
    [Fact]
    public void SplitStripsUserAndPasswordFromUrl()
    {
        var parts = RtspUrlCredentials.Split("rtsp://admin:secret@192.168.1.20:554/stream");
        parts.UrlWithoutCredentials.Must().Be("rtsp://192.168.1.20:554/stream");
        parts.Username.Must().Be("admin");
        parts.Password.Must().Be("secret");
    }

    [Fact]
    public void SplitKeepsUrlsWithoutUserinfo()
    {
        var parts = RtspUrlCredentials.Split("rtsp://192.168.1.20:554/stream");
        parts.UrlWithoutCredentials.Must().Be("rtsp://192.168.1.20:554/stream");
        parts.Username.VerifyNullable().BeNull();
        parts.Password.VerifyNullable().BeNull();
    }

    [Fact]
    public void RedactHidesEmbeddedCredentials()
    {
        RtspUrlCredentials.Redact("rtsp://admin:s3cret@192.168.1.20:554/stream")
            .Must()
            .Be("rtsp://***@192.168.1.20:554/stream");
    }

    [Fact]
    public void DisplayMasksWhenCredentialsAreStoredSeparately()
    {
        RtspUrlCredentials.Display("rtsp://192.168.1.20:554/stream", hasStoredCredentials: true)
            .Must()
            .Be("rtsp://***@192.168.1.20:554/stream");
    }

    [Fact]
    public void MeaningfulUserInfoIgnoresRedactionPlaceholder()
    {
        RtspUrlCredentials.MeaningfulUserInfo("***").VerifyNullable().BeNull();
        RtspUrlCredentials.MeaningfulUserInfo("admin").Must().Be("admin");
    }

    [Fact]
    public void SplitHandlesPasswordContainingAtSign()
    {
        var parts = RtspUrlCredentials.Split("rtsp://admin:p@ss@cam.local/live");
        parts.UrlWithoutCredentials.Must().Be("rtsp://cam.local/live");
        parts.Username.Must().Be("admin");
        parts.Password.Must().Be("p@ss");
    }
}
