using NidusVision.Core.Inference;

namespace NidusVision.Web.Inference;

public sealed class DetectionPresenceTracker
{
    public const int MissesBeforeClose = 3;

    private readonly Dictionary<Guid, CameraPresence> _cameras = [];

    public PresenceUpdate Observe(
        Guid cameraId,
        IReadOnlyList<BoundingBox> boxes,
        DateTimeOffset at)
    {
        if (!_cameras.TryGetValue(cameraId, out var state))
        {
            state = new CameraPresence();
            _cameras[cameraId] = state;
        }

        if (boxes.Count > 0)
        {
            var confidence = boxes.Max(box => box.Confidence);
            if (state.OpenEventId is null)
            {
                state.OpenEventId = Guid.CreateVersion7();
                state.StartUtc = at;
                state.EndUtc = at;
                state.MaxConfidence = confidence;
                state.Boxes = boxes;
                state.Misses = 0;
                return new PresenceUpdate(PresenceKind.Started, state.OpenEventId.Value, state.StartUtc, state.EndUtc, state.MaxConfidence, state.Boxes);
            }

            state.EndUtc = at;
            state.MaxConfidence = Math.Max(state.MaxConfidence, confidence);
            state.Boxes = boxes;
            state.Misses = 0;
            return new PresenceUpdate(PresenceKind.Extended, state.OpenEventId.Value, state.StartUtc, state.EndUtc, state.MaxConfidence, state.Boxes);
        }

        if (state.OpenEventId is { } openId)
        {
            state.Misses++;
            if (state.Misses >= MissesBeforeClose)
            {
                var closed = new PresenceUpdate(PresenceKind.Closed, openId, state.StartUtc, state.EndUtc, state.MaxConfidence, state.Boxes);
                state.OpenEventId = null;
                state.Misses = 0;
                return closed;
            }
        }

        return PresenceUpdate.None;
    }

    private sealed class CameraPresence
    {
        public Guid? OpenEventId { get; set; }
        public DateTimeOffset StartUtc { get; set; }
        public DateTimeOffset EndUtc { get; set; }
        public float MaxConfidence { get; set; }
        public IReadOnlyList<BoundingBox> Boxes { get; set; } = [];
        public int Misses { get; set; }
    }
}

public enum PresenceKind
{
    None,
    Started,
    Extended,
    Closed
}

public sealed record PresenceUpdate(
    PresenceKind Kind,
    Guid EventId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    float Confidence,
    IReadOnlyList<BoundingBox> Boxes)
{
    public static PresenceUpdate None { get; } = new(PresenceKind.None, Guid.Empty, default, default, 0, []);
}
