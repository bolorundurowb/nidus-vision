namespace NidusVision.Streaming;

public sealed class ReconnectBackoff(TimeProvider time)
{
    /// <summary>A run that stayed up at least this long counts as healthy, so the next failure starts the backoff over.</summary>
    public static readonly TimeSpan StableRunThreshold = TimeSpan.FromSeconds(60);

    public TimeSpan DelayForAttempt(int attempt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(attempt);
        var seconds = Math.Min(60, Math.Pow(2, Math.Min(attempt, 6)));
        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>Timestamp to pass to <see cref="NextAttempt(int, long?)"/> once the process has started.</summary>
    public long StartRun() => time.GetTimestamp();

    /// <summary>
    /// Attempt number for the delay after a run ends. <paramref name="runStartedTimestamp"/> is null
    /// when the run failed before the process started.
    /// </summary>
    public int NextAttempt(int attempt, long? runStartedTimestamp) =>
        NextAttemptAfter(attempt, runStartedTimestamp is { } started ? time.GetElapsedTime(started) : (TimeSpan?)null);

    /// <summary>
    /// Escalates the attempt count while runs keep failing fast, and drops back to the first
    /// attempt after a run that stayed up for <see cref="StableRunThreshold"/>. Without the reset,
    /// the count only grows, so a camera that had a few blips days ago would wait the full
    /// minute on every later reconnect.
    /// </summary>
    public static int NextAttemptAfter(int attempt, TimeSpan? ranFor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(attempt);
        if (ranFor is { } duration && duration >= StableRunThreshold)
        {
            return 1;
        }

        return attempt >= int.MaxValue - 1 ? attempt : attempt + 1;
    }
}
