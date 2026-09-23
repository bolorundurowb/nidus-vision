namespace NidusVision.Core.Cameras;

/// <summary>A recorded segment reduced to what the camera stat calculations need.</summary>
public sealed record SegmentStatSample(DateTimeOffset StartUtc, DateTimeOffset EndUtc, long ByteSize);

/// <summary>
/// Derives the bitrate and retention numbers shown per camera. Both are measured from the
/// recorded segments rather than from what the camera advertises, so they stay honest when a
/// stream drops or throttles. A null result means "not enough data yet".
/// </summary>
public static class CameraStreamStats
{
    /// <summary>Samples shorter than this cannot produce a meaningful average.</summary>
    private static readonly TimeSpan MinimumBitrateWindow = TimeSpan.FromSeconds(30);

    public static string? Bitrate(IEnumerable<SegmentStatSample> samples, DateTimeOffset now)
    {
        long bytes = 0;
        var window = TimeSpan.Zero;
        foreach (var sample in samples)
        {
            var end = sample.EndUtc < now ? sample.EndUtc : now;
            var duration = end - sample.StartUtc;
            if (duration <= TimeSpan.Zero || sample.ByteSize <= 0)
            {
                continue;
            }

            bytes += sample.ByteSize;
            window += duration;
        }

        if (bytes <= 0 || window < MinimumBitrateWindow)
        {
            return null;
        }

        var bitsPerSecond = bytes * 8d / window.TotalSeconds;
        return bitsPerSecond >= 1_000_000
            ? $"{bitsPerSecond / 1_000_000:0.#} Mbps"
            : $"{Math.Max(1, Math.Round(bitsPerSecond / 1_000)):0} kbps";
    }

    /// <summary>How far back recorded footage currently reaches for a camera.</summary>
    public static string? Retention(DateTimeOffset? oldestSegmentStartUtc, DateTimeOffset now)
    {
        if (oldestSegmentStartUtc is not { } oldest || oldest > now)
        {
            return null;
        }

        var span = now - oldest;
        if (span.TotalDays >= 1)
        {
            return Plural((int)span.TotalDays, "day");
        }

        if (span.TotalHours >= 1)
        {
            return Plural((int)span.TotalHours, "hour");
        }

        return span.TotalMinutes >= 1 ? Plural((int)span.TotalMinutes, "minute") : "< 1 minute";
    }

    private static string Plural(int value, string unit) => value == 1 ? $"1 {unit}" : $"{value} {unit}s";
}
