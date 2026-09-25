using NidusVision.Core.Inference;

namespace NidusVision.Web.Inference;

public sealed record DetectionFrame(
    Guid CameraId,
    DateTimeOffset CapturedAt,
    byte[] Rgb24,
    int Width,
    int Height,
    int SourceWidth,
    int SourceHeight);

public sealed class DetectionFrameBroker
{
    private readonly Dictionary<Guid, DetectionFrame> _latest = [];
    private readonly Lock _gate = new();

    public void Publish(DetectionFrame frame)
    {
        lock (_gate)
        {
            _latest[frame.CameraId] = frame;
        }
    }

    public IReadOnlyList<DetectionFrame> Drain()
    {
        lock (_gate)
        {
            if (_latest.Count == 0)
            {
                return [];
            }

            var frames = _latest.Values.ToArray();
            _latest.Clear();
            return frames;
        }
    }
}
