using NidusVision.Inference;

namespace NidusVision.Tests;

public sealed class FrameMotionGateTests
{
    private const int FrameSize = 640;
    private const int WideSourceWidth = 1920;
    private const int WideSourceHeight = 1080;
    private const int ShortSourceHeight = 80;

    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-27T12:00:00Z");

    [Fact]
    public void FirstFrameAndSourceSizeChangeRunAndReplaceTheReference()
    {
        var gate = new FrameMotionGate();
        var camera = Guid.CreateVersion7();
        var frame = Blank();

        gate.ShouldRun(camera, frame, WideSourceHeight, Start, presenceOpen: false).Must().BeTrue();
        gate.Remember(camera, WideSourceHeight, Start);

        gate.ShouldRun(camera, frame, ShortSourceHeight, Start.AddSeconds(1), presenceOpen: false).Must().BeTrue();
        gate.Remember(camera, ShortSourceHeight, Start.AddSeconds(1));

        gate.ShouldRun(camera, frame, ShortSourceHeight, Start.AddSeconds(2), presenceOpen: false).Must().BeFalse();
        gate.ShouldRun(camera, frame, WideSourceHeight, Start.AddSeconds(3), presenceOpen: false).Must().BeTrue();
    }

    [Fact]
    public void IdenticalContentSkipsWhenIdleHeartbeatIsNotDue()
    {
        var gate = new FrameMotionGate();
        var camera = Guid.CreateVersion7();
        var frame = Blank();
        gate.ShouldRun(camera, frame, WideSourceHeight, Start, presenceOpen: false).Must().BeTrue();
        gate.Remember(camera, WideSourceHeight, Start);

        gate.ShouldRun(camera, frame, WideSourceHeight, Start.AddSeconds(1), presenceOpen: false).Must().BeFalse();
    }

    [Fact]
    public void CellFractionTripsAtAboutEightPercentAndIgnoresOneSample()
    {
        var samples = ListSamples(WideSourceHeight);
        var cell = samples
            .GroupBy(sample => sample.Cell)
            .Select(group => group.ToList())
            .First(group => ChangedSamplesToTrip(group.Count) == 4 && group.Count >= FrameMotionGate.MinCellSamples);
        (4 * 2).Must().BeLessThan(cell.Count);

        var gate = new FrameMotionGate();
        var camera = Guid.CreateVersion7();
        var baseline = Blank();
        gate.ShouldRun(camera, baseline, WideSourceHeight, Start, presenceOpen: false).Must().BeTrue();
        gate.Remember(camera, WideSourceHeight, Start);

        var one = Blank();
        Paint(one, cell[0]);
        gate.ShouldRun(camera, one, WideSourceHeight, Start.AddSeconds(1), presenceOpen: false).Must().BeFalse();

        var tripped = Blank();
        for (var i = 0; i < 4; i++)
        {
            Paint(tripped, cell[i]);
        }

        gate.ShouldRun(camera, tripped, WideSourceHeight, Start.AddSeconds(2), presenceOpen: false).Must().BeTrue();
    }

    [Fact]
    public void OneSampleInAShortCellDoesNotTrip()
    {
        var samples = ListSamples(ShortSourceHeight);
        var cell = samples
            .GroupBy(sample => sample.Cell)
            .Select(group => group.ToList())
            .First(group => group.Count is > 0 and < FrameMotionGate.MinCellSamples);
        (samples.Count * FrameMotionGate.GlobalChangedPercent).Must().BeGreaterThan(100);

        var gate = new FrameMotionGate();
        var camera = Guid.CreateVersion7();
        var baseline = Blank();
        gate.ShouldRun(camera, baseline, ShortSourceHeight, Start, presenceOpen: false).Must().BeTrue();
        gate.Remember(camera, ShortSourceHeight, Start);

        var noisy = Blank();
        Paint(noisy, cell[0]);
        gate.ShouldRun(camera, noisy, ShortSourceHeight, Start.AddSeconds(1), presenceOpen: false).Must().BeFalse();
    }

    [Fact]
    public void LetterboxPaddingDoesNotCount()
    {
        var bounds = FrameMotionGate.ContentBounds.For(FrameSize, WideSourceWidth, WideSourceHeight);
        bounds.Y.Must().BeGreaterThanOrEqualTo(FrameMotionGate.MinCellSamples);

        var gate = new FrameMotionGate();
        var camera = Guid.CreateVersion7();
        var baseline = Blank();
        gate.ShouldRun(camera, baseline, WideSourceHeight, Start, presenceOpen: false).Must().BeTrue();
        gate.Remember(camera, WideSourceHeight, Start);

        var padded = Blank();
        for (var y = 0; y < bounds.Y; y++)
        {
            for (var x = 0; x < FrameSize; x++)
            {
                Paint(padded, x, y);
            }
        }

        gate.ShouldRun(camera, padded, WideSourceHeight, Start.AddSeconds(1), presenceOpen: false).Must().BeFalse();
    }

    [Fact]
    public void SubThresholdStepsAccumulateAgainstTheLastInferredFrame()
    {
        var cell = ListSamples(WideSourceHeight)
            .GroupBy(sample => sample.Cell)
            .Select(group => group.ToList())
            .First(group => ChangedSamplesToTrip(group.Count) == 4);
        var gate = new FrameMotionGate();
        var camera = Guid.CreateVersion7();
        var baseline = Blank();
        gate.ShouldRun(camera, baseline, WideSourceHeight, Start, presenceOpen: false).Must().BeTrue();
        gate.Remember(camera, WideSourceHeight, Start);

        for (var changed = 1; changed < 4; changed++)
        {
            var frame = Blank();
            for (var i = 0; i < changed; i++)
            {
                Paint(frame, cell[i]);
            }

            var at = Start.AddSeconds(changed);
            (at - Start).Must().BeLessThan(FrameMotionGate.IdleHeartbeat);
            gate.ShouldRun(camera, frame, WideSourceHeight, at, presenceOpen: false).Must().BeFalse();
        }

        var tripped = Blank();
        for (var i = 0; i < 4; i++)
        {
            Paint(tripped, cell[i]);
        }

        var tripAt = Start.AddSeconds(4);
        (tripAt - Start).Must().BeLessThan(FrameMotionGate.IdleHeartbeat);
        gate.ShouldRun(camera, tripped, WideSourceHeight, tripAt, presenceOpen: false).Must().BeTrue();
    }

    [Fact]
    public void HeartbeatAndBackwardClockRunOnAnUnchangedFrame()
    {
        var gate = new FrameMotionGate();
        var camera = Guid.CreateVersion7();
        var frame = Blank();
        gate.ShouldRun(camera, frame, WideSourceHeight, Start, presenceOpen: false).Must().BeTrue();
        gate.Remember(camera, WideSourceHeight, Start);

        gate.ShouldRun(camera, frame, WideSourceHeight, Start.AddSeconds(9), presenceOpen: false).Must().BeFalse();
        gate.ShouldRun(camera, frame, WideSourceHeight, Start.AddSeconds(10), presenceOpen: false).Must().BeTrue();

        gate.Remember(camera, WideSourceHeight, Start);
        gate.ShouldRun(camera, frame, WideSourceHeight, Start.AddSeconds(1), presenceOpen: true).Must().BeFalse();
        gate.ShouldRun(camera, frame, WideSourceHeight, Start.AddSeconds(2), presenceOpen: true).Must().BeTrue();

        gate.Remember(camera, WideSourceHeight, Start);
        gate.ShouldRun(camera, frame, WideSourceHeight, Start, presenceOpen: false).Must().BeTrue();
        gate.ShouldRun(camera, frame, WideSourceHeight, Start.AddSeconds(-1), presenceOpen: false).Must().BeTrue();
    }

    [Fact]
    public void SkipDoesNotMoveTheHeartbeatClock()
    {
        var gate = new FrameMotionGate();
        var camera = Guid.CreateVersion7();
        var frame = Blank();
        gate.ShouldRun(camera, frame, WideSourceHeight, Start, presenceOpen: true).Must().BeTrue();
        gate.Remember(camera, WideSourceHeight, Start);

        gate.ShouldRun(camera, frame, WideSourceHeight, Start.AddSeconds(1), presenceOpen: true).Must().BeFalse();
        gate.ShouldRun(camera, frame, WideSourceHeight, Start.AddSeconds(2), presenceOpen: true).Must().BeTrue();
    }

    [Fact]
    public void CamerasKeepSeparateReferences()
    {
        var gate = new FrameMotionGate();
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        var quiet = Blank();
        var busy = Blank();
        foreach (var sample in ListSamples(WideSourceHeight).Take(80))
        {
            Paint(busy, sample);
        }

        gate.ShouldRun(first, quiet, WideSourceHeight, Start, presenceOpen: false).Must().BeTrue();
        gate.Remember(first, WideSourceHeight, Start);
        gate.ShouldRun(second, busy, WideSourceHeight, Start, presenceOpen: false).Must().BeTrue();
        gate.Remember(second, WideSourceHeight, Start);

        gate.ShouldRun(first, quiet, WideSourceHeight, Start.AddSeconds(1), presenceOpen: false).Must().BeFalse();
    }

    private static int ChangedSamplesToTrip(int count)
    {
        var needed = 1;
        while (needed * 100 < count * FrameMotionGate.CellChangedPercent)
        {
            needed++;
        }

        return needed;
    }

    private static List<SamplePoint> ListSamples(int sourceHeight)
    {
        var samples = new List<SamplePoint>();
        var cursor = FrameMotionGate.Samples(FrameSize, FrameSize, WideSourceWidth, sourceHeight);
        while (cursor.MoveNext())
        {
            samples.Add(new SamplePoint(cursor.X, cursor.Y, cursor.Cell));
        }

        return samples;
    }

    private static byte[] Blank() => new byte[FrameSize * FrameSize * 3];

    private static void Paint(byte[] rgb, SamplePoint sample) => Paint(rgb, sample.X, sample.Y);

    private static void Paint(byte[] rgb, int x, int y)
    {
        var pixel = ((y * FrameSize) + x) * 3;
        rgb[pixel] = 255;
        rgb[pixel + 1] = 255;
        rgb[pixel + 2] = 255;
    }

    private readonly record struct SamplePoint(int X, int Y, int Cell);
}

file static class FrameMotionGateTestExtensions
{
    public static bool ShouldRun(
        this FrameMotionGate gate,
        Guid cameraId,
        byte[] rgb,
        int sourceHeight,
        DateTimeOffset at,
        bool presenceOpen) =>
        gate.ShouldRunModel(
            cameraId,
            rgb,
            640,
            640,
            1920,
            sourceHeight,
            at,
            presenceOpen);

    public static void Remember(this FrameMotionGate gate, Guid cameraId, int sourceHeight, DateTimeOffset at) =>
        gate.Remember(cameraId, 640, 640, 1920, sourceHeight, at);
}
