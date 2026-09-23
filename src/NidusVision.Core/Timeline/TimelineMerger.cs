namespace NidusVision.Core.Timeline;

public readonly record struct TimeInterval(DateTimeOffset Start, DateTimeOffset End, bool Human);

public static class TimelineMerger
{
    public static IReadOnlyList<TimeInterval> Merge(IEnumerable<TimeInterval> intervals)
    {
        var ordered = intervals
            .Where(i => i.End > i.Start)
            .OrderBy(i => i.Start)
            .ThenBy(i => i.End)
            .ToArray();
        if (ordered.Length == 0)
        {
            return [];
        }

        var merged = new List<TimeInterval> { ordered[0] };
        foreach (var next in ordered.AsSpan(1))
        {
            var last = merged[^1];
            if (next.Start <= last.End && next.Human == last.Human)
            {
                merged[^1] = last with { End = next.End > last.End ? next.End : last.End };
            }
            else
            {
                merged.Add(next);
            }
        }

        return merged;
    }

    public static double ToPercent(DateTimeOffset value, DateTimeOffset windowStart, TimeSpan window) =>
        window <= TimeSpan.Zero ? 0 : Math.Clamp((value - windowStart).TotalMilliseconds / window.TotalMilliseconds * 100, 0, 100);
}
