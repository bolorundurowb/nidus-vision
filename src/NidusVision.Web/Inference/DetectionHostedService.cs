using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NidusVision.Core.Inference;
using NidusVision.Core.Models;
using NidusVision.Data;
using NidusVision.Inference;
using NidusVision.Web.Events;
using NidusVision.Web.Hubs;

namespace NidusVision.Web.Inference;

public sealed class DetectionHostedService(
    IServiceScopeFactory scopes,
    HumanDetector detector,
    EventArtifactStore eventArtifacts,
    IHubContext<DetectionHub> alerts,
    ILogger<DetectionHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var settings = await db.AppSettings.AsNoTracking().OrderBy(s => s.Id).FirstAsync(stoppingToken);
                if (!settings.InferenceEnabled)
                {
                    continue;
                }

                _ = detector.Detect([], 0, 0, settings.ConfidenceThreshold);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Inference tick skipped.");
            }
        }
    }

    public async Task PersistDetectionAsync(AppDbContext db, Guid cameraId, float confidence, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        var detection = await PersistDetectionWithArtifactsAsync(
            db,
            eventArtifacts,
            cameraId,
            confidence,
            start,
            end,
            cancellationToken);
        var cameraName = await db.Cameras.AsNoTracking()
            .Where(c => c.Id == cameraId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Camera";
        await alerts.Clients.All.SendAsync(
            "alert",
            new DetectionAlert(detection.Id, cameraId, cameraName, confidence, start),
            cancellationToken);
    }

    public static async Task<DetectionEvent> PersistDetectionWithArtifactsAsync(
        AppDbContext db,
        EventArtifactStore eventArtifacts,
        Guid cameraId,
        float confidence,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        var detection = new DetectionEvent
        {
            CameraId = cameraId,
            StartUtc = start,
            EndUtc = end,
            Confidence = confidence,
        };
        db.DetectionEvents.Add(detection);

        var segments = await db.RecordingSegments.Where(s => s.CameraId == cameraId).ToListAsync(cancellationToken);
        var overlapping = segments
            .Where(segment => DetectionOverlap.Overlaps(segment.StartUtc, segment.EndUtc, start, end))
            .OrderBy(segment => segment.StartUtc)
            .ToList();
        foreach (var segment in overlapping)
        {
            segment.HasHuman = true;
        }

        detection.SegmentIdsJson = System.Text.Json.JsonSerializer.Serialize(overlapping.Select(s => s.Id));
        var source = overlapping.FirstOrDefault(s => File.Exists(s.Path));
        if (source is not null)
        {
            detection.ClipPath = await eventArtifacts.SaveClipAsync(detection, source.Path, source.StartUtc, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return detection;
    }
}
