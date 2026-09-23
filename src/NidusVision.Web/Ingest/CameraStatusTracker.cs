using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using NidusVision.Core.Models;
using NidusVision.Web.Hubs;

namespace NidusVision.Web.Ingest;

public sealed class CameraStatusTracker(IHubContext<CameraStatusHub> hub)
{
    private readonly ConcurrentDictionary<Guid, CameraStatus> _statuses = new();

    public async Task SetAsync(Guid cameraId, CameraStatus status, CancellationToken cancellationToken)
    {
        _statuses[cameraId] = status;
        var recording = _statuses.Count(pair => pair.Value is CameraStatus.Recording);
        await hub.Clients.All.SendAsync("status", new CameraStatusMessage(cameraId, status.ToString().ToLowerInvariant(), recording), cancellationToken);
    }
}
