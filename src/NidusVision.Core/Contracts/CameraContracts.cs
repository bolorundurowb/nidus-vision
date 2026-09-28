namespace NidusVision.Core.Contracts;

public sealed record CameraWriteRequest(
    string Name,
    string? Location,
    bool Enabled,
    string MainRtspUrl,
    string? SubRtspUrl,
    string? Username,
    string? Password,
    string Transport,
    bool ClearCredentials = false,
    bool? RecordingEnabled = null);

public sealed record CameraResponse(
    Guid Id,
    string Name,
    string Location,
    bool Enabled,
    string MainRtspUrl,
    string? SubRtspUrl,
    string? Username,
    bool HasPassword,
    string Transport,
    string Status,
    string? Resolution,
    int? Fps,
    string? Bitrate,
    string? Retention,
    bool RecordingEnabled);

public sealed record ProbeResult(bool Ok, string Message, string? Resolution, int? Fps);
