using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace NidusVision.Web.Hubs;

[Authorize]
public sealed class DetectionHub : Hub;

public sealed record DetectionAlert(Guid EventId, Guid CameraId, string CameraName, float Confidence, DateTimeOffset At);
