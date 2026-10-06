namespace NidusVision.Core.Options;

public static class SegmentDurationLimits
{
    public const int MinSegmentDurationSeconds = 5 * 60;
    public const int MaxSegmentDurationSeconds = 30 * 60;
    public const int DefaultSegmentDurationSeconds = 15 * 60;

    public static int ClampSegmentDuration(int seconds) => Math.Clamp(seconds, MinSegmentDurationSeconds, MaxSegmentDurationSeconds);
}
