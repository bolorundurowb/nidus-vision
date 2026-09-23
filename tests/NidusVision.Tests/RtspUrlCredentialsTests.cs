using NidusVision.Core.Security;

namespace NidusVision.Tests;

public sealed class RtspUrlCredentialsTests
{
    [Fact]
    public void Split_strips_user_and_password_from_url()
    {
        var parts = RtspUrlCredentials.Split("rtsp://admin:secret@192.168.1.20:554/stream");
        parts.UrlWithoutCredentials.Must().Be("rtsp://192.168.1.20:554/stream");
        parts.Username.Must().Be("admin");
        parts.Password.Must().Be("secret");
    }

    [Fact]
    public void Split_keeps_urls_without_userinfo()
    {
        var parts = RtspUrlCredentials.Split("rtsp://192.168.1.20:554/stream");
        parts.UrlWithoutCredentials.Must().Be("rtsp://192.168.1.20:554/stream");
        parts.Username.VerifyNullable().BeNull();
        parts.Password.VerifyNullable().BeNull();
    }

    [Fact]
    public void Redact_hides_embedded_credentials()
    {
        RtspUrlCredentials.Redact("rtsp://admin:s3cret@192.168.1.20:554/stream")
            .Must()
            .Be("rtsp://***@192.168.1.20:554/stream");
    }

    [Fact]
    public void Display_masks_when_credentials_are_stored_separately()
    {
        RtspUrlCredentials.Display("rtsp://192.168.1.20:554/stream", hasStoredCredentials: true)
            .Must()
            .Be("rtsp://***@192.168.1.20:554/stream");
    }

    [Fact]
    public void MeaningfulUserInfo_ignores_redaction_placeholder()
    {
        RtspUrlCredentials.MeaningfulUserInfo("***").VerifyNullable().BeNull();
        RtspUrlCredentials.MeaningfulUserInfo("admin").Must().Be("admin");
    }

    [Fact]
    public void Split_handles_password_containing_at_sign()
    {
        var parts = RtspUrlCredentials.Split("rtsp://admin:p@ss@cam.local/live");
        parts.UrlWithoutCredentials.Must().Be("rtsp://cam.local/live");
        parts.Username.Must().Be("admin");
        parts.Password.Must().Be("p@ss");
    }
}
