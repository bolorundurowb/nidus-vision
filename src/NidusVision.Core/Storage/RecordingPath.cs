using System.Globalization;

namespace NidusVision.Core.Storage;

public static class RecordingPath
{
    /// <summary>
    /// Poster frames live beside their segment so retention removes both together, and so the
    /// "*.mp4" scan that indexes untracked recordings never picks them up.
    /// </summary>
    public static string ThumbnailFor(string recordingPath) => Path.ChangeExtension(recordingPath, ".jpg");

    public static bool TryParseStart(string path, out DateTimeOffset start) =>
        DateTimeOffset.TryParseExact(
            Path.GetFileNameWithoutExtension(path),
            "yyyyMMdd'T'HHmmss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out start);
}
