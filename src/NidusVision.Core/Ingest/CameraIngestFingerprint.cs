using NidusVision.Core.Models;

namespace NidusVision.Core.Ingest;

public static class CameraIngestFingerprint
{
    public static string From(Camera camera) =>
        string.Join('|',
            camera.Enabled ? "1" : "0",
            camera.MainRtspUrl,
            (int)camera.Transport,
            camera.Username ?? "",
            camera.PasswordProtected ?? "");
}
