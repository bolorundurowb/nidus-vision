namespace NidusVision.Inference;

public readonly record struct LetterboxTransform(
    float Gain,
    int PadX,
    int PadY,
    int OriginalWidth,
    int OriginalHeight,
    int InputSize)
{
    public const int DefaultInputSize = 640;
    public const byte PadValue = 114;

    public static LetterboxTransform For(int originalWidth, int originalHeight, int inputSize = DefaultInputSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(originalWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(originalHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(inputSize);

        var gain = Math.Min(inputSize / (float)originalWidth, inputSize / (float)originalHeight);
        var resizedW = Math.Max(1, (int)Math.Round(originalWidth * gain));
        var resizedH = Math.Max(1, (int)Math.Round(originalHeight * gain));
        var padX = (int)Math.Round((inputSize - resizedW) / 2f - 0.1f);
        var padY = (int)Math.Round((inputSize - resizedH) / 2f - 0.1f);
        return new(gain, padX, padY, originalWidth, originalHeight, inputSize);
    }
}
