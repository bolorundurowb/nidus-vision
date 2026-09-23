using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NidusVision.Core.Inference;
using NidusVision.Core.Models;
using NidusVision.Data;
using NidusVision.Inference;

namespace NidusVision.Web.Inference;

public sealed class DetectionHostedService(
    IServiceScopeFactory scopes,
    HumanDetector detector,
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
                var settings = await db.AppSettings.AsNoTracking().FirstAsync(stoppingToken);
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

    public static async Task PersistDetectionAsync(AppDbContext db, Guid cameraId, float confidence, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        db.DetectionEvents.Add(new DetectionEvent
        {
            CameraId = cameraId,
            StartUtc = start,
            EndUtc = end,
            Confidence = confidence,
        });
        var segments = await db.RecordingSegments.Where(s => s.CameraId == cameraId).ToListAsync(cancellationToken);
        foreach (var segment in segments)
        {
            if (DetectionOverlap.Overlaps(segment.StartUtc, segment.EndUtc, start, end))
            {
                segment.HasHuman = true;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
