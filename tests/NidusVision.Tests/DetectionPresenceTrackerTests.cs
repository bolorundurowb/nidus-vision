using NidusVision.Core.Inference;
using NidusVision.Web.Inference;

namespace NidusVision.Tests;

public sealed class DetectionPresenceTrackerTests
{
    [Fact]
    public void ConsecutiveHitsOpenThenExtendOneEvent()
    {
        var tracker = new DetectionPresenceTracker();
        var camera = Guid.CreateVersion7();
        var start = DateTimeOffset.Parse("2026-09-25T12:00:00Z");
        var box = new BoundingBox(1, 2, 3, 4, 0.8f);

        var opened = tracker.Observe(camera, [box], start);
        var extended = tracker.Observe(camera, [box with { Confidence = 0.9f }], start.AddSeconds(1));

        opened.Kind.Must().Be(PresenceKind.Started);
        extended.Kind.Must().Be(PresenceKind.Extended);
        extended.EventId.Must().Be(opened.EventId);
        extended.Confidence.Must().Be(0.9f);
    }

    [Fact]
    public void MissesCloseTheOpenEvent()
    {
        var tracker = new DetectionPresenceTracker();
        var camera = Guid.CreateVersion7();
        var start = DateTimeOffset.Parse("2026-09-25T12:00:00Z");
        var opened = tracker.Observe(camera, [new BoundingBox(1, 1, 2, 2, 0.7f)], start);

        PresenceUpdate last = PresenceUpdate.None;
        for (var i = 1; i <= DetectionPresenceTracker.MissesBeforeClose; i++)
        {
            last = tracker.Observe(camera, [], start.AddSeconds(i));
        }

        last.Kind.Must().Be(PresenceKind.Closed);
        last.EventId.Must().Be(opened.EventId);
    }
}
