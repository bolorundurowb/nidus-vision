using NidusVision.Core.Inference;

namespace NidusVision.Inference;

public interface IHumanDetector
{
    bool IsAvailable { get; }

    IReadOnlyList<BoundingBox> Detect(
        ReadOnlySpan<byte> rgb24,
        int width,
        int height,
        int sourceWidth,
        int sourceHeight,
        float threshold);
}
