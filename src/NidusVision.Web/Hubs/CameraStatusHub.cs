using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace NidusVision.Web.Hubs;

[Authorize]
public sealed class CameraStatusHub : Hub;

public sealed record CameraStatusMessage(Guid CameraId, string Status, int RecordingCount);
