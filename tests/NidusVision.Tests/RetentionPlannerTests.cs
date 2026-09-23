using NidusVision.Core.Retention;

namespace NidusVision.Tests;

public sealed class RetentionPlannerTests
{
    [Fact]
    public void Purges_unmarked_before_human_by_age()
    {
        var now = DateTimeOffset.Parse("2026-09-23T00:00:00Z");
        var unmarked = new SegmentRetentionInfo(Guid.CreateVersion7(), now.AddDays(-10), false, 10, "a.mp4");
        var human = new SegmentRetentionInfo(Guid.CreateVersion7(), now.AddDays(-10), true, 10, "b.mp4");
        var result = RetentionPlanner.SelectPurge([unmarked, human], now, TimeSpan.FromDays(7), TimeSpan.FromDays(30), null);
        Assert.Contains(unmarked, result);
        Assert.DoesNotContain(human, result);
    }

    [Fact]
    public void Gb_cap_drops_oldest_unmarked_first()
    {
        var now = DateTimeOffset.Parse("2026-09-23T00:00:00Z");
        var old = new SegmentRetentionInfo(Guid.CreateVersion7(), now.AddHours(-2), false, 80, "old.mp4");
        var newer = new SegmentRetentionInfo(Guid.CreateVersion7(), now.AddHours(-1), false, 80, "new.mp4");
        var result = RetentionPlanner.SelectPurge([old, newer], now, TimeSpan.FromDays(7), TimeSpan.FromDays(30), 100);
        Assert.Equal(old.Id, Assert.Single(result).Id);
    }

    [Fact]
    public void Gb_cap_keeps_recent_human_clips()
    {
        var now = DateTimeOffset.Parse("2026-09-23T00:00:00Z");
        var human = new SegmentRetentionInfo(Guid.CreateVersion7(), now.AddHours(-1), true, 200, "h.mp4");
        var result = RetentionPlanner.SelectPurge([human], now, TimeSpan.FromDays(7), TimeSpan.FromDays(30), 10);
        Assert.Empty(result);
    }
}
