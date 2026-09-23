namespace NidusVision.Core.Retention;

public sealed record SegmentRetentionInfo(Guid Id, DateTimeOffset EndUtc, bool HasHuman, long ByteSize, string Path);

public static class RetentionPlanner
{
    public static IReadOnlyList<SegmentRetentionInfo> SelectPurge(
        IReadOnlyList<SegmentRetentionInfo> segments,
        DateTimeOffset now,
        TimeSpan generalLifetime,
        TimeSpan detectionLifetime,
        long? maxStorageBytes)
    {
        var expired = new List<SegmentRetentionInfo>();
        foreach (var segment in segments)
        {
            var lifetime = segment.HasHuman ? detectionLifetime : generalLifetime;
            if (now - segment.EndUtc >= lifetime)
            {
                expired.Add(segment);
            }
        }

        if (maxStorageBytes is not { } cap)
        {
            return expired;
        }

        var remaining = segments.Except(expired).ToList();
        var used = remaining.Sum(s => s.ByteSize);
        if (used <= cap)
        {
            return expired;
        }

        foreach (var segment in remaining.OrderBy(s => s.EndUtc))
        {
            if (used <= cap)
            {
                break;
            }

            expired.Add(segment);
            used -= segment.ByteSize;
        }

        return expired;
    }
}
