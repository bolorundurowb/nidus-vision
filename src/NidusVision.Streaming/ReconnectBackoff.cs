namespace NidusVision.Streaming;

public sealed class ReconnectBackoff(TimeProvider time)
{
    public TimeSpan DelayForAttempt(int attempt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(attempt);
        var seconds = Math.Min(60, Math.Pow(2, Math.Min(attempt, 6)));
        _ = time;
        return TimeSpan.FromSeconds(seconds);
    }
}
