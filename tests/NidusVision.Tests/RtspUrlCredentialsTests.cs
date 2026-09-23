using NidusVision.Core.Security;

namespace NidusVision.Tests;

public sealed class RtspUrlCredentialsTests
{
    [Fact]
    public void Split_strips_user_and_password_from_url()
    {
        var parts = RtspUrlCredentials.Split("rtsp://admin:secret@192.168.1.20:554/stream");
        Assert.Equal("rtsp://192.168.1.20:554/stream", parts.UrlWithoutCredentials);
        Assert.Equal("admin", parts.Username);
        Assert.Equal("secret", parts.Password);
    }

    [Fact]
    public void Split_keeps_urls_without_userinfo()
    {
        var parts = RtspUrlCredentials.Split("rtsp://192.168.1.20:554/stream");
        Assert.Equal("rtsp://192.168.1.20:554/stream", parts.UrlWithoutCredentials);
        Assert.Null(parts.Username);
        Assert.Null(parts.Password);
    }

    [Fact]
    public void Redact_hides_embedded_credentials()
    {
        Assert.Equal(
            "rtsp://***@192.168.1.20:554/stream",
            RtspUrlCredentials.Redact("rtsp://admin:s3cret@192.168.1.20:554/stream"));
    }

    [Fact]
    public void Display_masks_when_credentials_are_stored_separately()
    {
        Assert.Equal(
            "rtsp://***@192.168.1.20:554/stream",
            RtspUrlCredentials.Display("rtsp://192.168.1.20:554/stream", hasStoredCredentials: true));
    }

    [Fact]
    public void MeaningfulUserInfo_ignores_redaction_placeholder()
    {
        Assert.Null(RtspUrlCredentials.MeaningfulUserInfo("***"));
        Assert.Equal("admin", RtspUrlCredentials.MeaningfulUserInfo("admin"));
    }

    [Fact]
    public void Split_handles_password_containing_at_sign()
    {
        var parts = RtspUrlCredentials.Split("rtsp://admin:p@ss@cam.local/live");
        Assert.Equal("rtsp://cam.local/live", parts.UrlWithoutCredentials);
        Assert.Equal("admin", parts.Username);
        Assert.Equal("p@ss", parts.Password);
    }
}
