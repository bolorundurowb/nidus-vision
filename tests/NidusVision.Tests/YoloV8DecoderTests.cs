using NidusVision.Core.Inference;
using NidusVision.Inference;

namespace NidusVision.Tests;

public sealed class YoloV8DecoderTests
{
    [Fact]
    public void DecodeMapsChannelsFirstBoxesBackThroughLetterbox()
    {
        var transform = LetterboxTransform.For(100, 100, 100);
        var output = new float[5];
        output[0] = 50;
        output[1] = 50;
        output[2] = 20;
        output[3] = 10;
        output[4] = 0.9f;

        var boxes = YoloV8Decoder.Decode(output, [1, 5, 1], 0.5f, transform);

        boxes.Must().HaveCount(1);
        boxes[0].X.Must().Be(40);
        boxes[0].Y.Must().Be(45);
        boxes[0].Width.Must().Be(20);
        boxes[0].Height.Must().Be(10);
        boxes[0].Confidence.Must().Be(0.9f);
    }

    [Fact]
    public void DecodeReadsChannelsLastLayout()
    {
        var transform = LetterboxTransform.For(100, 100, 100);
        var output = new float[] { 50, 50, 10, 10, 0.8f };

        var boxes = YoloV8Decoder.Decode(output, [1, 1, 5], 0.5f, transform);

        boxes.Must().HaveCount(1);
        boxes[0].Width.Must().Be(10);
        boxes[0].Confidence.Must().Be(0.8f);
    }

    [Fact]
    public void DecodeDropsScoresBelowThreshold()
    {
        var transform = LetterboxTransform.For(100, 100, 100);
        var output = new float[] { 50, 50, 10, 10, 0.2f };

        YoloV8Decoder.Decode(output, [1, 5, 1], 0.5f, transform).Must().BeEmpty();
    }

    [Fact]
    public void LetterboxInverseRemovesPadding()
    {
        var transform = LetterboxTransform.For(1920, 1080);
        var cx = transform.PadX + 50 * transform.Gain;
        var cy = transform.PadY + 60 * transform.Gain;
        var w = 40 * transform.Gain;
        var h = 80 * transform.Gain;
        var output = new float[] { cx, cy, w, h, 0.99f };

        var boxes = YoloV8Decoder.Decode(output, [1, 5, 1], 0.5f, transform);

        boxes.Must().HaveCount(1);
        Math.Abs(boxes[0].X - 30).Must().BeLessThan(1.5f);
        Math.Abs(boxes[0].Y - 20).Must().BeLessThan(1.5f);
        Math.Abs(boxes[0].Width - 40).Must().BeLessThan(1.5f);
        Math.Abs(boxes[0].Height - 80).Must().BeLessThan(1.5f);
    }

    [Fact]
    public void PackNchwNormalizesRgbPlanes()
    {
        byte[] rgb = [255, 0, 0, 0, 255, 0];
        var tensor = RgbLetterbox.PackNchw(rgb, 2, 1);

        tensor.Must().HaveCount(6);
        tensor[0].Must().Be(1f);
        tensor[1].Must().Be(0f);
        tensor[2].Must().Be(0f);
        tensor[3].Must().Be(1f);
        tensor[4].Must().Be(0f);
        tensor[5].Must().Be(0f);
    }

    [Fact]
    public void HumanDetectorReturnsEmptyWhenModelIsMissing()
    {
        var previous = Environment.GetEnvironmentVariable(PersonModelPath.EnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(PersonModelPath.EnvironmentVariable, Path.Combine(Path.GetTempPath(), "missing-person.onnx"));
            using var detector = new HumanDetector(new SilentLogger(), contentRoot: Path.GetTempPath());
            detector.IsAvailable.Must().BeFalse();
            detector.Detect([1, 2, 3], 1, 1, 1, 1, 0.5f).Must().BeEmpty();
        }
        finally
        {
            Environment.SetEnvironmentVariable(PersonModelPath.EnvironmentVariable, previous);
        }
    }

    private sealed class SilentLogger : Microsoft.Extensions.Logging.ILogger<HumanDetector>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}
