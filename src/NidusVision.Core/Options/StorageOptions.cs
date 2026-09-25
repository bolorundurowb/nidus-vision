namespace NidusVision.Core.Options;

public sealed class StorageOptions
{
    public const int DefaultSegmentDurationSeconds = 15 * 60;
    public const int MaxSegmentDurationSeconds = 30 * 60;

    public string DataDirectory { get; set; } = "data";
    public string RecordingsDirectory { get; set; } = "recordings";
    public int SegmentDurationSeconds { get; set; } = DefaultSegmentDurationSeconds;
    public string DatabaseFileName { get; set; } = "nidus.db";

    public int EffectiveSegmentDurationSeconds =>
        Math.Clamp(SegmentDurationSeconds, 1, MaxSegmentDurationSeconds);
}
