using System.Globalization;
using NidusVision.Core.Models;

namespace NidusVision.Core.Ingest;

public static class CameraIngestFingerprint
{
    public static string From(Camera camera, bool inferenceEnabled = true, float sampleFps = 1f) =>
        string.Join('|',
            camera.Enabled ? "1" : "0",
            camera.MainRtspUrl,
            (int)camera.Transport,
            camera.Username ?? "",
            camera.PasswordProtected ?? "",
            inferenceEnabled ? "1" : "0",
            sampleFps.ToString("0.###", CultureInfo.InvariantCulture));
}
