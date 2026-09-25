namespace NidusVision.Inference;

public static class RgbLetterbox
{
    public static byte[] Apply(ReadOnlySpan<byte> rgb, int width, int height, LetterboxTransform transform)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(rgb.Length, checked(width * height * 3));
        if (width != transform.OriginalWidth || height != transform.OriginalHeight)
        {
            throw new ArgumentException("Frame dimensions must match the letterbox transform.");
        }

        var size = transform.InputSize;
        var output = new byte[size * size * 3];
        output.AsSpan().Fill(LetterboxTransform.PadValue);

        var gain = transform.Gain;
        var resizedW = Math.Max(1, (int)Math.Round(width * gain));
        var resizedH = Math.Max(1, (int)Math.Round(height * gain));
        var padX = transform.PadX;
        var padY = transform.PadY;

        for (var y = 0; y < resizedH; y++)
        {
            var srcY = Math.Clamp((int)Math.Round((y + 0.5f) / gain - 0.5f), 0, height - 1);
            for (var x = 0; x < resizedW; x++)
            {
                var srcX = Math.Clamp((int)Math.Round((x + 0.5f) / gain - 0.5f), 0, width - 1);
                var src = (srcY * width + srcX) * 3;
                var dstX = padX + x;
                var dstY = padY + y;
                if ((uint)dstX >= (uint)size || (uint)dstY >= (uint)size)
                {
                    continue;
                }

                var dst = (dstY * size + dstX) * 3;
                output[dst] = rgb[src];
                output[dst + 1] = rgb[src + 1];
                output[dst + 2] = rgb[src + 2];
            }
        }

        return output;
    }

    public static float[] PackNchw(ReadOnlySpan<byte> rgb, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(rgb.Length, checked(width * height * 3));
        var plane = width * height;
        var tensor = new float[3 * plane];
        for (var i = 0; i < plane; i++)
        {
            var src = i * 3;
            tensor[i] = rgb[src] / 255f;
            tensor[plane + i] = rgb[src + 1] / 255f;
            tensor[2 * plane + i] = rgb[src + 2] / 255f;
        }

        return tensor;
    }
}
