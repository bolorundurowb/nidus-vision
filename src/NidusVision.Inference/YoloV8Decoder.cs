using NidusVision.Core.Inference;

namespace NidusVision.Inference;

public static class YoloV8Decoder
{
    public static IReadOnlyList<BoundingBox> Decode(
        float[] output,
        int[] dimensions,
        float threshold,
        LetterboxTransform transform)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(dimensions);

        var (channelsFirst, channels, predictions) = Describe(dimensions);
        if (channels < 5 || predictions <= 0)
        {
            throw new InvalidOperationException(
                $"Unexpected YOLO output shape [{string.Join(',', dimensions)}]. Expected [1, 4+nc, N] or [1, N, 4+nc].");
        }

        var detections = new List<BoundingBox>(Math.Min(predictions, 64));
        for (var i = 0; i < predictions; i++)
        {
            var score = At(output, channelsFirst, channels, predictions, 4, i);
            if (score < threshold)
            {
                continue;
            }

            var cx = At(output, channelsFirst, channels, predictions, 0, i);
            var cy = At(output, channelsFirst, channels, predictions, 1, i);
            var w = At(output, channelsFirst, channels, predictions, 2, i);
            var h = At(output, channelsFirst, channels, predictions, 3, i);

            var x = (cx - w / 2f - transform.PadX) / transform.Gain;
            var y = (cy - h / 2f - transform.PadY) / transform.Gain;
            var width = w / transform.Gain;
            var height = h / transform.Gain;

            var clipped = Clip(x, y, width, height, transform.OriginalWidth, transform.OriginalHeight);
            if (clipped is null)
            {
                continue;
            }

            detections.Add(clipped.Value with { Confidence = score });
        }

        return detections;
    }

    internal static (bool ChannelsFirst, int Channels, int Predictions) Describe(int[] dimensions)
    {
        var dims = dimensions.Length == 4 ? dimensions[1..] : dimensions;
        if (dims.Length == 2)
        {
            return PreferLayout(dims[0], dims[1]);
        }

        if (dims.Length == 3)
        {
            return PreferLayout(dims[1], dims[2]);
        }

        throw new InvalidOperationException($"Unsupported output rank {dimensions.Length}.");
    }

    private static (bool ChannelsFirst, int Channels, int Predictions) PreferLayout(int a, int b)
    {
        var aLooksLikeChannels = LooksLikeChannelCount(a);
        var bLooksLikeChannels = LooksLikeChannelCount(b);
        if (aLooksLikeChannels && !bLooksLikeChannels)
        {
            return (true, a, b);
        }

        if (bLooksLikeChannels && !aLooksLikeChannels)
        {
            return (false, b, a);
        }

        return a <= b ? (true, a, b) : (false, b, a);
    }

    private static bool LooksLikeChannelCount(int value) => value is >= 5 and <= 144;

    private static float At(float[] output, bool channelsFirst, int channels, int predictions, int channel, int index) =>
        channelsFirst
            ? output[channel * predictions + index]
            : output[index * channels + channel];

    private static BoundingBox? Clip(float x, float y, float width, float height, int imageWidth, int imageHeight)
    {
        var x2 = x + width;
        var y2 = y + height;
        x = Math.Clamp(x, 0, imageWidth);
        y = Math.Clamp(y, 0, imageHeight);
        x2 = Math.Clamp(x2, 0, imageWidth);
        y2 = Math.Clamp(y2, 0, imageHeight);
        width = x2 - x;
        height = y2 - y;
        if (width < 1 || height < 1)
        {
            return null;
        }

        return new BoundingBox(x, y, width, height, 0);
    }
}
