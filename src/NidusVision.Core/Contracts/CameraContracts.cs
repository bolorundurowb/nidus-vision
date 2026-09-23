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
    string? RoiJson);

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
    string? RoiJson,
    string? Resolution,
    int? Fps,
    string? Bitrate,
    string? Retention);

public sealed record ProbeResult(bool Ok, string Message, string? Resolution, int? Fps);
