using System.Globalization;

namespace NidusVision.Core.Storage;

public static class RecordingPath
{
    public static bool TryParseStart(string path, out DateTimeOffset start) =>
        DateTimeOffset.TryParseExact(
            Path.GetFileNameWithoutExtension(path),
            "yyyyMMdd'T'HHmmss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out start);
}
