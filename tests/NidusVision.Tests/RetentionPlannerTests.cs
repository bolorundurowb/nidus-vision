using NidusVision.Core.Retention;

namespace NidusVision.Tests;

public sealed class RetentionPlannerTests
{
    [Fact]
    public void Time_only_deletes_expired_segments()
    {
        var now = DateTimeOffset.Parse("2026-09-23T00:00:00Z");
        var unmarked = new SegmentRetentionInfo(Guid.CreateVersion7(), now.AddDays(-10), false, 10, "a.mp4");
        var human = new SegmentRetentionInfo(Guid.CreateVersion7(), now.AddDays(-10), true, 10, "b.mp4");

        var result = RetentionPlanner.SelectPurge([unmarked, human], now, TimeSpan.FromDays(7), TimeSpan.FromDays(30), null);

        result.Must().Contain(unmarked);
        result.Must().NotContain(human);
    }

    [Fact]
    public void Storage_only_deletes_oldest_segments_until_under_cap()
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
    public void Combined_policy_applies_time_and_storage_limits()
    {
        var now = DateTimeOffset.Parse("2026-09-23T00:00:00Z");
        var expired = new SegmentRetentionInfo(Guid.CreateVersion7(), now.AddDays(-10), false, 80, "expired.mp4");
        var oldestRetained = new SegmentRetentionInfo(Guid.CreateVersion7(), now.AddDays(-3), true, 80, "oldest-retained.mp4");
        var newest = new SegmentRetentionInfo(Guid.CreateVersion7(), now.AddDays(-1), false, 80, "newest.mp4");

        var result = RetentionPlanner.SelectPurge(
            [newest, expired, oldestRetained],
            now,
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(30),
            100);

        result.Select(segment => segment.Id).Must().BeSequenceEqual([expired.Id, oldestRetained.Id]);
    }

    [Fact]
    public void Under_storage_cap_does_not_delete_unexpired_segments()
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
