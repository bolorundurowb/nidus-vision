namespace NidusVision.Core.Cameras;

/// <summary>Camera URLs are handed to FFmpeg as <c>-i</c>, which opens any protocol it knows, including local files.</summary>
public static class CameraUrl
{
    public static bool IsSupported(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme.Equals("rtsp", StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals("rtsps", StringComparison.OrdinalIgnoreCase))
        && !string.IsNullOrEmpty(uri.Host);

    public static void EnsureSupported(string? url, string paramName = "url")
    {
        if (!IsSupported(url))
        {
            throw new ArgumentException("Camera URL must be an rtsp:// or rtsps:// address.", paramName);
        }
    }
}
