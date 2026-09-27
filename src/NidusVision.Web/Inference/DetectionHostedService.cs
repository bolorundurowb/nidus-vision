using System.Text.Json;
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
    IHumanDetector detector,
    DetectionFrameBroker frames,
    ILogger<DetectionHostedService> logger) : BackgroundService
{
    private readonly DetectionPresenceTracker _presence = new();
    private readonly FrameMotionGate _motion = new();
    private readonly Dictionary<Guid, MotionWindow> _motionWindows = [];
    private DateTimeOffset _motionLoggedAt = DateTimeOffset.UtcNow;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                LogMotionWindows();
                var pending = frames.Drain();
                if (pending.Count == 0 || !detector.IsAvailable)
                {
                    continue;
                }

                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var settings = await db.AppSettings.AsNoTracking().OrderBy(s => s.Id).FirstAsync(stoppingToken);
                if (!settings.InferenceEnabled)
                {
                    continue;
                }

                foreach (var frame in pending)
                {
                    await HandleFrameAsync(db, frame, settings.ConfidenceThreshold, stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(ex, "Inference tick skipped.");
            }
        }
    }

    internal async Task HandleFrameAsync(
        AppDbContext db,
        DetectionFrame frame,
        float threshold,
        CancellationToken cancellationToken)
    {
        var camera = await db.Cameras.AsNoTracking()
            .Where(c => c.Id == frame.CameraId)
            .Select(c => new { c.Id })
            .FirstOrDefaultAsync(cancellationToken);
        if (camera is null)
        {
            return;
        }

        var presenceOpen = _presence.HasOpenInterval(frame.CameraId);
        if (!_motion.ShouldRunModel(
                frame.CameraId,
                frame.Rgb24,
                frame.Width,
                frame.Height,
                frame.SourceWidth,
                frame.SourceHeight,
                frame.CapturedAt,
                presenceOpen))
        {
            NoteMotion(frame.CameraId, ran: false);
            return;
        }

        var boxes = detector.Detect(
            frame.Rgb24,
            frame.Width,
            frame.Height,
            frame.SourceWidth,
            frame.SourceHeight,
            threshold);
        _motion.Remember(
            frame.CameraId,
            frame.Width,
            frame.Height,
            frame.SourceWidth,
            frame.SourceHeight,
            frame.CapturedAt);
        NoteMotion(frame.CameraId, ran: true);
        var update = _presence.Observe(frame.CameraId, boxes, frame.CapturedAt);
        if (update.Kind is PresenceKind.None)
        {
            return;
        }

        if (update.Kind is PresenceKind.Started)
        {
            await PersistDetectionAsync(
                db,
                update.EventId,
                frame.CameraId,
                update.Confidence,
                update.StartUtc,
                update.EndUtc,
                update.Boxes,
                cancellationToken);
            return;
        }

        await UpdateDetectionAsync(db, update, cancellationToken);
    }

    internal bool HasOpenInterval(Guid cameraId) => _presence.HasOpenInterval(cameraId);

    private void NoteMotion(Guid cameraId, bool ran)
    {
        if (!_motionWindows.TryGetValue(cameraId, out var window))
        {
            window = new MotionWindow();
            _motionWindows[cameraId] = window;
        }

        if (ran)
        {
            window.Runs++;
        }
        else
        {
            window.Skips++;
        }
    }

    private void LogMotionWindows()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _motionLoggedAt < TimeSpan.FromMinutes(1) || _motionWindows.Count == 0)
        {
            return;
        }

        foreach (var (cameraId, window) in _motionWindows)
        {
            logger.LogInformation(
                "Motion gate for camera {CameraId}: {Runs} model runs and {Skips} skips.",
                cameraId,
                window.Runs,
                window.Skips);
        }

        _motionWindows.Clear();
        _motionLoggedAt = now;
    }

    private sealed class MotionWindow
    {
        public int Runs { get; set; }
        public int Skips { get; set; }
    }

    public async Task PersistDetectionAsync(
        AppDbContext db,
        Guid cameraId,
        float confidence,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        await PersistDetectionAsync(
            db,
            Guid.CreateVersion7(),
            cameraId,
            confidence,
            start,
            end,
            [],
            cancellationToken);
    }

    private async Task PersistDetectionAsync(
        AppDbContext db,
        Guid eventId,
        Guid cameraId,
        float confidence,
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlyList<BoundingBox> boxes,
        CancellationToken cancellationToken)
    {
        await PersistDetectionIntervalAsync(
            db,
            cameraId,
            confidence,
            start,
            end,
            boxes,
            eventId,
            cancellationToken);
    }

    private static async Task UpdateDetectionAsync(AppDbContext db, PresenceUpdate update, CancellationToken cancellationToken)
    {
        var detection = await db.DetectionIntervals.FirstOrDefaultAsync(e => e.Id == update.EventId, cancellationToken);
        if (detection is null)
        {
            return;
        }

        detection.EndUtc = update.EndUtc;
        detection.Confidence = update.Confidence;
        if (update.Boxes.Count > 0)
        {
            detection.BoundingBoxJson = JsonSerializer.Serialize(update.Boxes);
        }

        await MarkOverlappingSegmentsAsync(db, detection, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public static async Task<DetectionInterval> PersistDetectionIntervalAsync(
        AppDbContext db,
        Guid cameraId,
        float confidence,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken) =>
        await PersistDetectionIntervalAsync(
            db,
            cameraId,
            confidence,
            start,
            end,
            [],
            Guid.CreateVersion7(),
            cancellationToken);

    public static async Task<DetectionInterval> PersistDetectionIntervalAsync(
        AppDbContext db,
        Guid cameraId,
        float confidence,
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlyList<BoundingBox> boxes,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var detection = new DetectionInterval
        {
            Id = eventId,
            CameraId = cameraId,
            StartUtc = start,
            EndUtc = end,
            Confidence = confidence,
            BoundingBoxJson = boxes.Count == 0 ? null : JsonSerializer.Serialize(boxes),
        };
        db.DetectionIntervals.Add(detection);
        await MarkOverlappingSegmentsAsync(db, detection, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return detection;
    }

    private static async Task MarkOverlappingSegmentsAsync(
        AppDbContext db,
        DetectionInterval detection,
        CancellationToken cancellationToken)
    {
        var segments = await db.RecordingSegments.Where(s => s.CameraId == detection.CameraId).ToListAsync(cancellationToken);
        var overlapping = segments
            .Where(segment => DetectionOverlap.Overlaps(segment.StartUtc, segment.EndUtc, detection.StartUtc, detection.EndUtc))
            .OrderBy(segment => segment.StartUtc)
            .ToList();
        foreach (var segment in overlapping)
        {
            segment.HasHuman = true;
        }

    }
}
