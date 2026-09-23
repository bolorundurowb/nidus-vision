using NidusVision.Core.Inference;

namespace NidusVision.Tests;

public sealed class DetectionOverlapTests
{
    [Fact]
    public void Overlaps_when_ranges_intersect()
    {
        var a = DateTimeOffset.Parse("2026-09-23T14:00:00Z");
        DetectionOverlap.Overlaps(a, a.AddMinutes(10), a.AddMinutes(9), a.AddMinutes(12)).Must().BeTrue();
        DetectionOverlap.Overlaps(a, a.AddMinutes(10), a.AddMinutes(11), a.AddMinutes(12)).Must().BeFalse();
    }

    [Fact]
    public void Nms_drops_heavy_overlap()
    {
        var kept = NonMaxSuppression.Filter(
        [
            new BoundingBox(0, 0, 10, 10, 0.9f),
            new BoundingBox(1, 1, 10, 10, 0.8f),
            new BoundingBox(50, 50, 5, 5, 0.7f),
        ], 0.3f);
        kept.Must().HaveCount(2);
    }
}
