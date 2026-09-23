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
        merged.Must().HaveCount(1);
        merged[0].End.Must().Be(start.AddMinutes(15));
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
        merged.Must().HaveCount(2);
    }

    [Fact]
    public void ToPercent_clamps_to_window()
    {
        var start = DateTimeOffset.Parse("2026-09-23T14:00:00Z");
        TimelineMerger.ToPercent(start.AddMinutes(-1), start, TimeSpan.FromHours(1)).Must().Be(0);
        TimelineMerger.ToPercent(start.AddMinutes(30), start, TimeSpan.FromHours(1)).Must().Be(50);
        TimelineMerger.ToPercent(start.AddHours(2), start, TimeSpan.FromHours(1)).Must().Be(100);
    }
}
