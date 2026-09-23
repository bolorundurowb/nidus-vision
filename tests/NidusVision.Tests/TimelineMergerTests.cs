using NidusVision.Core.Timeline;

namespace NidusVision.Tests;

public sealed class TimelineMergerTests
{
    [Fact]
    public void Merge_combines_overlapping_same_kind()
    {
        var start = DateTimeOffset.Parse("2026-09-23T14:00:00Z");
        var intervals = new TimeInterval[]
        {
            new(start, start.AddMinutes(10), false),
            new(start.AddMinutes(8), start.AddMinutes(15), false),
        };
        var merged = TimelineMerger.Merge(intervals);
        Assert.Single(merged);
        Assert.Equal(start.AddMinutes(15), merged[0].End);
    }

    [Fact]
    public void Merge_keeps_human_separate()
    {
        var start = DateTimeOffset.Parse("2026-09-23T14:00:00Z");
        var intervals = new TimeInterval[]
        {
            new(start, start.AddMinutes(20), false),
            new(start.AddMinutes(5), start.AddMinutes(6), true),
        };
        var merged = TimelineMerger.Merge(intervals);
        Assert.Equal(2, merged.Count);
    }

    [Fact]
    public void ToPercent_clamps_to_window()
    {
        var start = DateTimeOffset.Parse("2026-09-23T14:00:00Z");
        Assert.Equal(0, TimelineMerger.ToPercent(start.AddMinutes(-1), start, TimeSpan.FromHours(1)));
        Assert.Equal(50, TimelineMerger.ToPercent(start.AddMinutes(30), start, TimeSpan.FromHours(1)));
        Assert.Equal(100, TimelineMerger.ToPercent(start.AddHours(2), start, TimeSpan.FromHours(1)));
    }
}
