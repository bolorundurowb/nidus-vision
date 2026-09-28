using NidusVision.Core.Models;
using NidusVision.Core.Storage;

namespace NidusVision.Tests;

public sealed class RecordingLayoutTests : IDisposable
{
    private readonly TestDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void FinalizedRecordingMovesIntoItsUtcDayFolder()
    {
        var cameraDirectory = _directory.GetPath("camera");
        Directory.CreateDirectory(cameraDirectory);
        var recording = Path.Combine(cameraDirectory, "20260923T183000.mp4");
        var thumbnail = Path.Combine(cameraDirectory, "20260923T183000.jpg");
        var open = Path.Combine(cameraDirectory, "20260923T190000.mp4");
        File.WriteAllBytes(recording, [1, 2, 3, 4]);
        File.WriteAllBytes(thumbnail, [9]);
        File.WriteAllBytes(open, [5]);
        var nested = _directory.GetPath("camera", "2026", "09", "23");
        Directory.CreateDirectory(nested);
        var nestedFile = Path.Combine(nested, "20260923T120000.mp4");
        File.WriteAllBytes(nestedFile, [7]);

        var placed = RecordingLayout.Place(cameraDirectory, recording, thumbnail, isFinalized: true);
        var stayed = RecordingLayout.Place(cameraDirectory, open, null, isFinalized: false);
        var fromNested = RecordingLayout.Place(cameraDirectory, nestedFile, null, isFinalized: true);

        var day = Path.Combine(cameraDirectory, "2026-09-23");
        placed.Result.Must().Be(RecordingPlaceResult.Moved);
        placed.Path.Must().Be(Path.Combine(day, "20260923T183000.mp4"));
        placed.ThumbnailPath.Must().Be(Path.Combine(day, "20260923T183000.jpg"));
        File.Exists(recording).Must().BeFalse();
        File.Exists(placed.ThumbnailPath!).Must().BeTrue();
        stayed.Result.Must().Be(RecordingPlaceResult.Unchanged);
        File.Exists(open).Must().BeTrue();
        fromNested.Path.Must().Be(Path.Combine(day, "20260923T120000.mp4"));
        File.Exists(nestedFile).Must().BeFalse();
    }

    [Fact]
    public void DayFolderDoesNotOverwriteADifferentRecording()
    {
        var cameraDirectory = _directory.GetPath("camera");
        var day = Path.Combine(cameraDirectory, "2026-09-23");
        Directory.CreateDirectory(day);
        var destination = Path.Combine(day, "20260923T183000.mp4");
        var source = Path.Combine(cameraDirectory, "20260923T183000.mp4");
        File.WriteAllBytes(destination, [1, 2, 3, 4]);
        File.WriteAllBytes(source, [9]);

        var collided = RecordingLayout.Place(cameraDirectory, source, null, isFinalized: true);

        collided.Result.Must().Be(RecordingPlaceResult.Collided);
        File.Exists(source).Must().BeTrue();
        new FileInfo(destination).Length.Must().Be(4);

        File.WriteAllBytes(source, [1, 2, 3, 4]);
        var adopted = RecordingLayout.Place(cameraDirectory, source, null, isFinalized: true);
        adopted.Result.Must().Be(RecordingPlaceResult.Adopted);
        adopted.Path.Must().Be(destination);
        File.Exists(source).Must().BeFalse();
    }

    [Fact]
    public void PlacingAMissingSourceAdoptsTheExistingDestinationInsteadOfThrowing()
    {
        // Regression test for a startup crash loop: if an earlier pass moved a recording into its day
        // folder but the database was never updated with the new path (or the source file was later
        // removed by retention/cleanup), a later sort pass would still see the stale source path. It must
        // not throw just because that source no longer exists on disk.
        var cameraDirectory = _directory.GetPath("camera");
        var day = Path.Combine(cameraDirectory, "2026-09-23");
        Directory.CreateDirectory(day);
        var destination = Path.Combine(day, "20260923T183000.mp4");
        var source = Path.Combine(cameraDirectory, "20260923T183000.mp4");
        File.WriteAllBytes(destination, [1, 2, 3, 4]);

        var adopted = RecordingLayout.Place(cameraDirectory, source, null, isFinalized: true);

        adopted.Result.Must().Be(RecordingPlaceResult.Adopted);
        adopted.Path.Must().Be(destination);
        File.Exists(destination).Must().BeTrue();
    }

    [Fact]
    public void PlacingAMissingSourceMovesItsThumbnailIntoTheDayFolder()
    {
        var cameraDirectory = _directory.GetPath("camera");
        var day = Path.Combine(cameraDirectory, "2026-09-23");
        Directory.CreateDirectory(day);
        var destination = Path.Combine(day, "20260923T183000.mp4");
        var source = Path.Combine(cameraDirectory, "20260923T183000.mp4");
        var sourceThumb = Path.Combine(cameraDirectory, "20260923T183000.jpg");
        var destinationThumb = Path.Combine(day, "20260923T183000.jpg");
        File.WriteAllBytes(destination, [1, 2, 3, 4]);
        File.WriteAllBytes(sourceThumb, [9]);

        var adopted = RecordingLayout.Place(cameraDirectory, source, sourceThumb, isFinalized: true);

        adopted.Result.Must().Be(RecordingPlaceResult.Adopted);
        adopted.ThumbnailPath.Must().Be(destinationThumb);
        File.Exists(destinationThumb).Must().BeTrue();
        File.Exists(sourceThumb).Must().BeFalse();
    }

    [Fact]
    public void PlacingAMissingSourceDropsAThumbnailTheDayFolderAlreadyHas()
    {
        var cameraDirectory = _directory.GetPath("camera");
        var day = Path.Combine(cameraDirectory, "2026-09-23");
        Directory.CreateDirectory(day);
        var destination = Path.Combine(day, "20260923T183000.mp4");
        var source = Path.Combine(cameraDirectory, "20260923T183000.mp4");
        var sourceThumb = Path.Combine(cameraDirectory, "20260923T183000.jpg");
        var destinationThumb = Path.Combine(day, "20260923T183000.jpg");
        File.WriteAllBytes(destination, [1, 2, 3, 4]);
        File.WriteAllBytes(destinationThumb, [8]);
        File.WriteAllBytes(sourceThumb, [9]);

        var adopted = RecordingLayout.Place(cameraDirectory, source, sourceThumb, isFinalized: true);

        adopted.ThumbnailPath.Must().Be(destinationThumb);
        new FileInfo(destinationThumb).Length.Must().Be(1);
        File.Exists(sourceThumb).Must().BeFalse();
    }

    [Fact]
    public void ClaimingAPathAlreadyIndexedDropsTheDuplicateAndKeepsTheOwner()
    {
        var root = _directory.GetPath("claim");
        var destination = Path.Combine(root, "2026-09-23", "20260923T183000.mp4");
        var thumbnail = Path.Combine(root, "2026-09-23", "20260923T183000.jpg");
        var owner = new RecordingSegment
        {
            Path = destination,
            HasHuman = false,
        };
        var stale = new RecordingSegment
        {
            Path = Path.Combine(root, "20260923T183000.mp4"),
            HasHuman = true,
        };

        var claim = RecordingLayout.ClaimIndexedPath(stale, destination, thumbnail, [owner, stale]);

        claim.Changed.Must().BeTrue();
        claim.DropDuplicate.Must().BeTrue();
        owner.HasHuman.Must().BeTrue();
        owner.ThumbnailPath.Must().Be(thumbnail);
        stale.Path.Must().Be(Path.Combine(root, "20260923T183000.mp4"));
    }

    [Fact]
    public void ClaimingAFreePathUpdatesTheSegment()
    {
        var root = _directory.GetPath("claim-free");
        var destination = Path.Combine(root, "2026-09-23", "20260923T183000.mp4");
        var thumbnail = Path.Combine(root, "2026-09-23", "20260923T183000.jpg");
        var segment = new RecordingSegment
        {
            Path = Path.Combine(root, "20260923T183000.mp4"),
        };

        var claim = RecordingLayout.ClaimIndexedPath(segment, destination, thumbnail, [segment]);

        claim.Changed.Must().BeTrue();
        claim.DropDuplicate.Must().BeFalse();
        segment.Path.Must().Be(destination);
        segment.ThumbnailPath.Must().Be(thumbnail);
    }

    [Fact]
    public void RetargetIgnoresAFolderNameThatIsNotTheCameraDirectory()
    {
        var root = _directory.GetPath("recordings");
        Directory.CreateDirectory(root);
        var id = Guid.Parse("0123456789abcdef0123456789abcdef");
        var folder = id.ToString("N");
        var current = Path.Combine(root, folder + "-front");
        var middle = Path.Combine(root, "album", folder, "clip.mp4");
        var outside = _directory.GetPath("backup", folder, "clip.mp4");
        var stored = Path.Combine(root, folder, "clip.mp4");

        RecordingLayout.RetargetPath(middle, root, current).VerifyNullable().BeNull();
        RecordingLayout.RetargetPath(outside, root, current).VerifyNullable().BeNull();
        RecordingLayout.RetargetPath(stored, root, current).Must().Be(Path.Combine(current, "clip.mp4"));
    }

    [Fact]
    public void ResolveRenamesALegacyFolderAndLeavesABusyTargetAlone()
    {
        var root = _directory.GetPath("recordings");
        var id = Guid.Parse("0123456789abcdef0123456789abcdef");
        var legacy = Path.Combine(root, id.ToString("N"));
        Directory.CreateDirectory(legacy);
        File.WriteAllBytes(Path.Combine(legacy, "old.mp4"), [1]);

        var renamed = RecordingLayout.Resolve(root, id, "Front Door");

        var preferred = Path.Combine(root, id.ToString("N") + "-front-door");
        renamed.Directory.Must().Be(preferred);
        Directory.Exists(legacy).Must().BeFalse();
        File.Exists(Path.Combine(preferred, "old.mp4")).Must().BeTrue();

        Directory.CreateDirectory(legacy);
        File.WriteAllBytes(Path.Combine(legacy, "older.mp4"), [2]);
        File.WriteAllBytes(Path.Combine(preferred, "current.mp4"), [3]);
        var left = RecordingLayout.Resolve(root, id, "Front Door");
        left.Directory.Must().Be(preferred);
        File.Exists(Path.Combine(legacy, "older.mp4")).Must().BeTrue();
        File.Exists(Path.Combine(preferred, "current.mp4")).Must().BeTrue();
    }
}
