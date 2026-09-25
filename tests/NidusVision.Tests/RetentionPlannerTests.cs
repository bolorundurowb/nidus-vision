using NidusVision.Core.Retention;

namespace NidusVision.Tests;

public sealed class RetentionPlannerTests
{
    [Fact]
    public void TimeOnlyDeletesExpiredSegments()
    {
        var now = DateTimeOffset.Parse("2026-09-23T00:00:00Z");
        var unmarked = new SegmentRetentionInfo(Guid.CreateVersion7(), now.AddDays(-10), false, 10, "a.mp4");
        var human = new SegmentRetentionInfo(Guid.CreateVersion7(), now.AddDays(-10), true, 10, "b.mp4");

        var result = RetentionPlanner.SelectPurge([unmarked, human], now, TimeSpan.FromDays(7), TimeSpan.FromDays(30), null);

        result.Must().Contain(unmarked);
        result.Must().NotContain(human);
    }

    [Fact]
    public void StorageOnlyDeletesOldestSegmentsUntilUnderCap()
    {
        var now = DateTimeOffset.Parse("2026-09-23T00:00:00Z");
        var old = new SegmentRetentionInfo(Guid.CreateVersion7(), now.AddHours(-2), false, 80, "old.mp4");
        var newerHuman = new SegmentRetentionInfo(Guid.CreateVersion7(), now.AddHours(-1), true, 80, "new.mp4");

        var result = RetentionPlanner.SelectPurge(
            [newerHuman, old],
            now,
            TimeSpan.FromDays(365),
            TimeSpan.FromDays(365),
            100);

        result.Must().HaveCount(1);
        result[0].Id.Must().Be(old.Id);
    }

    [Fact]
    public void CombinedPolicyAppliesTimeAndStorageLimits()
    {
        var now = DateTimeOffset.Parse("2026-09-23T00:00:00Z");
        var expired = new SegmentRetentionInfo(Guid.CreateVersion7(), now.AddDays(-10), false, 80, "expired.mp4");
        var oldestDetection = new SegmentRetentionInfo(Guid.CreateVersion7(), now.AddDays(-3), true, 80, "oldest-detection.mp4");
        var newestUnmarked = new SegmentRetentionInfo(Guid.CreateVersion7(), now.AddDays(-1), false, 80, "newest-unmarked.mp4");

        var result = RetentionPlanner.SelectPurge(
            [newestUnmarked, expired, oldestDetection],
            now,
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(30),
            100);

        result.Select(segment => segment.Id).Must().BeSequenceEqual([expired.Id, newestUnmarked.Id]);
    }

    [Fact]
    public void StoragePrefersUnmarkedOverOlderDetectionsThenDeletesOldest()
    {
        var now = DateTimeOffset.Parse("2026-09-23T00:00:00Z");
        var olderDetection = new SegmentRetentionInfo(Guid.CreateVersion7(), now.AddHours(-3), true, 80, "older-detection.mp4");
        var newerUnmarked = new SegmentRetentionInfo(Guid.CreateVersion7(), now.AddHours(-1), false, 80, "newer-unmarked.mp4");
        var newestDetection = new SegmentRetentionInfo(Guid.CreateVersion7(), now.AddMinutes(-10), true, 80, "newest-detection.mp4");

        var result = RetentionPlanner.SelectPurge(
            [newestDetection, newerUnmarked, olderDetection],
            now,
            TimeSpan.FromDays(365),
            TimeSpan.FromDays(365),
            100);

        result.Select(segment => segment.Id).Must().BeSequenceEqual([newerUnmarked.Id, olderDetection.Id]);
    }

    [Fact]
    public void UnderStorageCapDoesNotDeleteUnexpiredSegments()
    {
        var now = DateTimeOffset.Parse("2026-09-23T00:00:00Z");
        var old = new SegmentRetentionInfo(Guid.CreateVersion7(), now.AddHours(-2), false, 40, "old.mp4");
        var newer = new SegmentRetentionInfo(Guid.CreateVersion7(), now.AddHours(-1), true, 50, "new.mp4");

        var result = RetentionPlanner.SelectPurge(
            [old, newer],
            now,
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(30),
            100);

        result.Must().BeEmpty();
    }
}
