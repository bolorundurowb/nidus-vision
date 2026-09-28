using System.Globalization;

namespace NidusVision.Core.Storage;

public static class RecordingPath
{
    public const int MaxSlugLength = 48;

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

    public static string CameraDirectoryName(Guid cameraId, string? name)
    {
        var id = cameraId.ToString("N");
        var slug = Slug(name);
        return slug.Length == 0 ? id : string.Concat(id, "-", slug);
    }

    public static string Slug(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "";
        }

        Span<char> buffer = stackalloc char[MaxSlugLength];
        var length = 0;
        var hyphen = false;
        foreach (var ch in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                if (hyphen && length > 0 && length < MaxSlugLength)
                {
                    buffer[length++] = '-';
                }

                hyphen = false;
                if (length >= MaxSlugLength)
                {
                    break;
                }

                buffer[length++] = ch;
            }
            else
            {
                hyphen = true;
            }
        }

        while (length > 0 && buffer[length - 1] == '-')
        {
            length--;
        }

        return new string(buffer[..length]);
    }

    public static bool TryParseCameraId(string? folderName, out Guid cameraId)
    {
        cameraId = default;
        if (string.IsNullOrEmpty(folderName) || folderName.Length < 32)
        {
            return false;
        }

        if (!Guid.TryParseExact(folderName.AsSpan(0, 32), "N", out cameraId))
        {
            return false;
        }

        return folderName.Length == 32 || folderName[32] == '-';
    }

    public static bool BelongsToCamera(string? folderName, Guid cameraId) =>
        TryParseCameraId(folderName, out var parsed) && parsed == cameraId;

    public static string DayFolderName(DateTimeOffset startUtc) =>
        startUtc.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static bool IsInDayFolder(string filePath)
    {
        if (!TryParseStart(filePath, out var start))
        {
            return false;
        }

        var parent = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(filePath) ?? ""));
        return string.Equals(parent, DayFolderName(start), StringComparison.Ordinal);
    }
}
