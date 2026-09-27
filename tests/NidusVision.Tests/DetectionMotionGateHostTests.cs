using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NidusVision.Core.Inference;
using NidusVision.Core.Models;
using NidusVision.Data;
using NidusVision.Inference;
using NidusVision.Web.Inference;

namespace NidusVision.Tests;

public sealed class DetectionMotionGateHostTests : SqliteTestBase
{
    private const int FrameSize = 640;
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-27T12:00:00Z");
    private static readonly BoundingBox Person = new(1, 2, 3, 4, 0.8f);

    [Fact]
    public async Task ThreeSkipsInsideTheOpenHeartbeatLeaveTheIntervalOpen()
    {
        await using var db = CreateContext();
        var camera = await AddCameraAsync(db);
        var detector = new ScriptedDetector();
        detector.Return(Person);
        var service = CreateService(detector);
        var frame = Blank();

        await service.HandleFrameAsync(db, Capture(camera, frame, Start), 0.5f, CancellationToken.None);
        for (var step = 1; step <= 3; step++)
        {
            var at = Start.AddMilliseconds(400 * step);
            (at - Start).Must().BeLessThan(FrameMotionGate.OpenIntervalHeartbeat);
            await service.HandleFrameAsync(db, Capture(camera, frame, at), 0.5f, CancellationToken.None);
        }

        detector.Calls.Must().Be(1);
        service.HasOpenInterval(camera).Must().BeTrue();
    }

    [Fact]
    public async Task OpenIntervalRunsAtThreeSecondsAndClosedIntervalDoesNot()
    {
        await using var db = CreateContext();
        var camera = await AddCameraAsync(db);
        var detector = new ScriptedDetector();
        detector.Return(Person);
        detector.Return(Person);
        detector.Return();
        detector.Return();
        detector.Return();
        var service = CreateService(detector);
        var frame = Blank();

        await service.HandleFrameAsync(db, Capture(camera, frame, Start), 0.5f, CancellationToken.None);
        await service.HandleFrameAsync(db, Capture(camera, frame, Start.AddSeconds(3)), 0.5f, CancellationToken.None);
        detector.Calls.Must().Be(2);
        service.HasOpenInterval(camera).Must().BeTrue();

        await service.HandleFrameAsync(db, Capture(camera, frame, Start.AddSeconds(5)), 0.5f, CancellationToken.None);
        await service.HandleFrameAsync(db, Capture(camera, frame, Start.AddSeconds(7)), 0.5f, CancellationToken.None);
        await service.HandleFrameAsync(db, Capture(camera, frame, Start.AddSeconds(9)), 0.5f, CancellationToken.None);
        service.HasOpenInterval(camera).Must().BeFalse();
        detector.Calls.Must().Be(5);

        await service.HandleFrameAsync(db, Capture(camera, frame, Start.AddSeconds(12)), 0.5f, CancellationToken.None);
        detector.Calls.Must().Be(5);
    }

    [Fact]
    public async Task SkipDoesNotMoveTheHeartbeatClock()
    {
        await using var db = CreateContext();
        var camera = await AddCameraAsync(db);
        var detector = new ScriptedDetector();
        detector.Return(Person);
        detector.Return(Person);
        var service = CreateService(detector);
        var frame = Blank();

        await service.HandleFrameAsync(db, Capture(camera, frame, Start), 0.5f, CancellationToken.None);
        await service.HandleFrameAsync(db, Capture(camera, frame, Start.AddSeconds(1)), 0.5f, CancellationToken.None);
        detector.Calls.Must().Be(1);

        await service.HandleFrameAsync(db, Capture(camera, frame, Start.AddSeconds(2)), 0.5f, CancellationToken.None);
        detector.Calls.Must().Be(2);
        service.HasOpenInterval(camera).Must().BeTrue();
    }

    [Fact]
    public async Task ThrownDetectDoesNotReplaceTheReference()
    {
        await using var db = CreateContext();
        var camera = await AddCameraAsync(db);
        var detector = new ScriptedDetector();
        detector.Return(Person);
        detector.ThrowNext();
        detector.Return(Person);
        var service = CreateService(detector);
        var quiet = Blank();
        var moved = Blank();
        foreach (var sample in SamplesInTrippingCell().Take(4))
        {
            var pixel = ((sample.Y * FrameSize) + sample.X) * 3;
            moved[pixel] = moved[pixel + 1] = moved[pixel + 2] = 255;
        }

        await service.HandleFrameAsync(db, Capture(camera, quiet, Start), 0.5f, CancellationToken.None);
        try
        {
            await service.HandleFrameAsync(db, Capture(camera, moved, Start.AddMilliseconds(500)), 0.5f, CancellationToken.None);
            throw new InvalidOperationException("Detect was expected to throw.");
        }
        catch (InvalidOperationException ex) when (ex.Message == "detect failed")
        {
        }

        detector.Calls.Must().Be(2);
        await service.HandleFrameAsync(db, Capture(camera, quiet, Start.AddMilliseconds(600)), 0.5f, CancellationToken.None);
        detector.Calls.Must().Be(2);

        await service.HandleFrameAsync(db, Capture(camera, moved, Start.AddMilliseconds(700)), 0.5f, CancellationToken.None);
        detector.Calls.Must().Be(3);
    }

    private static DetectionHostedService CreateService(ScriptedDetector detector) =>
        new(new UnusedScopes(), detector, new DetectionFrameBroker(), NullLogger<DetectionHostedService>.Instance);

    private static async Task<Guid> AddCameraAsync(AppDbContext db)
    {
        var camera = new Camera { Name = "Front", MainRtspUrl = "rtsp://cam/stream" };
        db.Cameras.Add(camera);
        await db.SaveChangesAsync();
        return camera.Id;
    }

    private static DetectionFrame Capture(Guid cameraId, byte[] rgb, DateTimeOffset at) =>
        new(cameraId, at, rgb, FrameSize, FrameSize, 1920, 1080);

    private static byte[] Blank() => new byte[FrameSize * FrameSize * 3];

    private static List<SamplePoint> SamplesInTrippingCell()
    {
        var samples = new List<SamplePoint>();
        var cursor = FrameMotionGate.Samples(FrameSize, FrameSize, 1920, 1080);
        while (cursor.MoveNext())
        {
            samples.Add(new SamplePoint(cursor.X, cursor.Y, cursor.Cell));
        }

        return samples
            .GroupBy(sample => sample.Cell)
            .Select(group => group.ToList())
            .First(group =>
            {
                var needed = 1;
                while (needed * 100 < group.Count * FrameMotionGate.CellChangedPercent)
                {
                    needed++;
                }

                return needed == 4;
            });
    }

    private readonly record struct SamplePoint(int X, int Y, int Cell);

    private sealed class UnusedScopes : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new NotSupportedException();
    }

    private sealed class ScriptedDetector : IHumanDetector
    {
        private readonly Queue<Func<IReadOnlyList<BoundingBox>>> _script = new();

        public int Calls { get; private set; }

        public bool IsAvailable => true;

        public void Return(params BoundingBox[] boxes) => _script.Enqueue(() => boxes);

        public void ThrowNext() => _script.Enqueue(() => throw new InvalidOperationException("detect failed"));

        public IReadOnlyList<BoundingBox> Detect(
            ReadOnlySpan<byte> rgb24,
            int width,
            int height,
            int sourceWidth,
            int sourceHeight,
            float threshold)
        {
            Calls++;
            return _script.Dequeue()();
        }
    }
}
