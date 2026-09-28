using NidusVision.Core.Models;

namespace NidusVision.Core.Storage;

public enum RecordingPlaceResult
{
    Unchanged,
    Moved,
    Adopted,
    Collided
}

public readonly record struct RecordingPlace(RecordingPlaceResult Result, string Path, string? ThumbnailPath);

public readonly record struct RecordingPathClaim(bool Changed, bool DropDuplicate);

public readonly record struct CameraDirectoryChoice(string Directory, string? RenamedFrom);

public static class RecordingLayout
{
    private static readonly Lock Gate = new();

    public static CameraDirectoryChoice Resolve(string recordingsRoot, Guid cameraId, string? cameraName, Action<string>? warn = null)
    {
        var root = Path.GetFullPath(recordingsRoot);
        Directory.CreateDirectory(root);
        var preferred = Path.Combine(root, RecordingPath.CameraDirectoryName(cameraId, cameraName));
        var matches = Directory.EnumerateDirectories(root)
            .Where(dir => RecordingPath.BelongsToCamera(Path.GetFileName(dir), cameraId))
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .ToList();
        var others = matches.Where(match => !SamePath(match, preferred)).ToList();

        if (others.Count == 0)
        {
            Directory.CreateDirectory(preferred);
            return new CameraDirectoryChoice(preferred, null);
        }

        if (others.Count > 1)
        {
            warn?.Invoke($"Camera {cameraId} has more than one recording folder. New files stay in the folder that already has footage.");
            var keep = matches.FirstOrDefault(HasEntries) ?? others[0];
            return new CameraDirectoryChoice(keep, null);
        }

        var other = others[0];
        var preferredHasFiles = Directory.Exists(preferred) && HasEntries(preferred);
        if (preferredHasFiles && HasEntries(other))
        {
            warn?.Invoke($"Camera {cameraId} already has files in both {Path.GetFileName(preferred)} and {Path.GetFileName(other)}. Leaving both in place.");
            return new CameraDirectoryChoice(preferred, null);
        }

        if (preferredHasFiles)
        {
            return new CameraDirectoryChoice(preferred, null);
        }

        try
        {
            if (Directory.Exists(preferred) && !HasEntries(preferred))
            {
                Directory.Delete(preferred);
            }

            Directory.Move(other, preferred);
            return new CameraDirectoryChoice(preferred, other);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warn?.Invoke($"Could not rename recordings for camera {cameraId} to {Path.GetFileName(preferred)}. {ex.Message}");
            return new CameraDirectoryChoice(other, null);
        }
    }

    /// <summary>
    /// Replaces the camera folder at the start of a path under <paramref name="recordingsRoot"/>.
    /// A path that only contains the folder name later in the string is left alone.
    /// </summary>
    public static string? RetargetPath(string? storedPath, string recordingsRoot, string cameraDirectory)
    {
        if (string.IsNullOrWhiteSpace(storedPath) || !StorageRoot.Contains(recordingsRoot, storedPath))
        {
            return null;
        }

        var full = Path.GetFullPath(storedPath);
        var current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(cameraDirectory));
        if (StorageRoot.Contains(current, full) || SamePath(full, current))
        {
            return null;
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(recordingsRoot));
        var relative = Path.GetRelativePath(root, full);
        var separator = relative.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        if (separator <= 0)
        {
            return null;
        }

        var folder = relative[..separator];
        if (!RecordingPath.TryParseCameraId(folder, out var storedCamera)
            || !RecordingPath.TryParseCameraId(Path.GetFileName(current), out var targetCamera)
            || storedCamera != targetCamera)
        {
            return null;
        }

        return Path.Combine(current, relative[(separator + 1)..]);
    }

    public static bool ShouldRetarget(string currentPath, string candidatePath)
    {
        var currentExists = File.Exists(currentPath);
        var candidateExists = File.Exists(candidatePath);
        if (candidateExists && currentExists)
        {
            return new FileInfo(currentPath).Length == new FileInfo(candidatePath).Length;
        }

        if (candidateExists)
        {
            return true;
        }

        return !currentExists;
    }

    public static string? CameraDirectoryFor(string recordingsRoot, string filePath)
    {
        if (!StorageRoot.Contains(recordingsRoot, filePath))
        {
            return null;
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(recordingsRoot));
        var relative = Path.GetRelativePath(root, Path.GetFullPath(filePath));
        var separator = relative.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        if (separator <= 0)
        {
            return null;
        }

        return Path.Combine(root, relative[..separator]);
    }

    public static RecordingPlace Place(string cameraDirectory, string recordingPath, string? thumbnailPath, bool isFinalized)
    {
        lock (Gate)
        {
            return PlaceCore(cameraDirectory, recordingPath, thumbnailPath, isFinalized);
        }
    }

    public static RecordingPathClaim ClaimIndexedPath(
        RecordingSegment segment,
        string placedPath,
        string? placedThumbnail,
        IEnumerable<RecordingSegment> siblings)
    {
        var owner = siblings.FirstOrDefault(other => other.Id != segment.Id && SamePath(other.Path, placedPath));
        if (owner is not null)
        {
            owner.HasHuman |= segment.HasHuman;
            if (string.IsNullOrEmpty(owner.ThumbnailPath) && !string.IsNullOrEmpty(placedThumbnail))
            {
                owner.ThumbnailPath = placedThumbnail;
            }

            return new RecordingPathClaim(Changed: true, DropDuplicate: true);
        }

        segment.Path = placedPath;
        segment.ThumbnailPath = placedThumbnail;
        return new RecordingPathClaim(Changed: true, DropDuplicate: false);
    }

    public static void RemoveEmptyChildDirectories(string cameraDirectory, IEnumerable<string> keepPaths)
    {
        if (!Directory.Exists(cameraDirectory))
        {
            return;
        }

        var root = Path.GetFullPath(cameraDirectory);
        var keep = new HashSet<string>(PathComparer);
        foreach (var path in keepPaths)
        {
            var dir = Path.GetDirectoryName(path);
            while (!string.IsNullOrEmpty(dir) && !SamePath(dir, root))
            {
                keep.Add(Path.GetFullPath(dir));
                dir = Path.GetDirectoryName(dir);
            }
        }

        var directories = Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
            .OrderByDescending(dir => dir.Length)
            .ToList();
        foreach (var dir in directories)
        {
            var full = Path.GetFullPath(dir);
            if (keep.Contains(full) || Directory.EnumerateFileSystemEntries(full).Any())
            {
                continue;
            }

            Directory.Delete(full);
        }
    }

    private static RecordingPlace PlaceCore(string cameraDirectory, string recordingPath, string? thumbnailPath, bool isFinalized)
    {
        var source = Path.GetFullPath(recordingPath);
        if (!isFinalized || !RecordingPath.TryParseStart(source, out var start) || RecordingPath.IsInDayFolder(source))
        {
            return new RecordingPlace(RecordingPlaceResult.Unchanged, source, thumbnailPath);
        }

        var destinationDirectory = Path.Combine(Path.GetFullPath(cameraDirectory), RecordingPath.DayFolderName(start));
        var destination = Path.Combine(destinationDirectory, Path.GetFileName(source));
        if (SamePath(source, destination))
        {
            return new RecordingPlace(RecordingPlaceResult.Unchanged, source, thumbnailPath);
        }

        Directory.CreateDirectory(destinationDirectory);
        var sourceThumb = ExistingThumbnail(source, thumbnailPath);
        var destinationThumb = RecordingPath.ThumbnailFor(destination);

        if (File.Exists(destination))
        {
            if (!File.Exists(source))
            {
                // An earlier pass may have moved the recording without updating the database.
                // The day-folder copy is the one to keep, and any leftover poster frame moves with it.
                return new RecordingPlace(
                    RecordingPlaceResult.Adopted,
                    destination,
                    AdoptThumbnail(sourceThumb, destinationThumb));
            }

            if (new FileInfo(source).Length != new FileInfo(destination).Length)
            {
                return new RecordingPlace(RecordingPlaceResult.Collided, source, thumbnailPath);
            }

            TryDelete(source);
            return new RecordingPlace(
                RecordingPlaceResult.Adopted,
                destination,
                AdoptThumbnail(sourceThumb, destinationThumb));
        }

        if (!TryMove(source, destination))
        {
            if (!File.Exists(source) && File.Exists(destination))
            {
                return new RecordingPlace(
                    RecordingPlaceResult.Moved,
                    destination,
                    File.Exists(destinationThumb) ? destinationThumb : thumbnailPath);
            }

            return new RecordingPlace(RecordingPlaceResult.Unchanged, source, thumbnailPath);
        }

        if (sourceThumb is not null && !File.Exists(destinationThumb))
        {
            TryMove(sourceThumb, destinationThumb);
        }

        return new RecordingPlace(
            RecordingPlaceResult.Moved,
            destination,
            File.Exists(destinationThumb) ? destinationThumb : null);
    }

    private static string? AdoptThumbnail(string? sourceThumb, string destinationThumb)
    {
        if (sourceThumb is not null && !SamePath(sourceThumb, destinationThumb))
        {
            if (!File.Exists(destinationThumb))
            {
                TryMove(sourceThumb, destinationThumb);
            }
            else
            {
                TryDelete(sourceThumb);
            }
        }

        return File.Exists(destinationThumb) ? destinationThumb : null;
    }

    private static string? ExistingThumbnail(string recordingPath, string? thumbnailPath)
    {
        if (!string.IsNullOrWhiteSpace(thumbnailPath) && File.Exists(thumbnailPath))
        {
            return Path.GetFullPath(thumbnailPath);
        }

        var beside = RecordingPath.ThumbnailFor(recordingPath);
        return File.Exists(beside) ? beside : null;
    }

    private static bool TryMove(string source, string destination)
    {
        try
        {
            File.Move(source, destination);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            /* the next pass can try again */
        }
    }

    private static bool HasEntries(string directory) =>
        Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any();

    private static bool SamePath(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), PathComparison);

    private static StringComparer PathComparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static StringComparison PathComparison { get; } =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
