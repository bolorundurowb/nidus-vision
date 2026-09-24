using NidusVision.Core.Timeline;

namespace NidusVision.Tests;

public sealed class TimelineMergerTests
{
    [Fact]
    public void MergeCombinesOverlappingSameKind()
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
    public void MergeKeepsHumanSeparate()
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
    public void MergeCombinesSameKindAroundInterleavedOtherKind()
    {
        var start = DateTimeOffset.Parse("2026-09-23T14:00:00Z");
        var intervals = new TimeInterval[]
        {
            new(start, start.AddMinutes(20), false),
            new(start.AddMinutes(5), start.AddMinutes(6), true),
            new(start.AddMinutes(15), start.AddMinutes(30), false),
        };
        var merged = TimelineMerger.Merge(intervals);
        merged.Must().HaveCount(2);
        merged[0].Human.Must().BeFalse();
        merged[0].Start.Must().Be(start);
        merged[0].End.Must().Be(start.AddMinutes(30));
        merged[1].Human.Must().BeTrue();
        merged[1].Start.Must().Be(start.AddMinutes(5));
        merged[1].End.Must().Be(start.AddMinutes(6));
    }

    [Fact]
    public void ToPercentClampsToWindow()
    {
        var start = DateTimeOffset.Parse("2026-09-23T14:00:00Z");
        TimelineMerger.ToPercent(start.AddMinutes(-1), start, TimeSpan.FromHours(1)).Must().Be(0);
        TimelineMerger.ToPercent(start.AddMinutes(30), start, TimeSpan.FromHours(1)).Must().Be(50);
        TimelineMerger.ToPercent(start.AddHours(2), start, TimeSpan.FromHours(1)).Must().Be(100);
    }
}
