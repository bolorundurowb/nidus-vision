namespace NidusVision.Core.Options;

public static class RetentionLimits
{
    public const int MinDays = 1;

    /// <summary>Ten years. Larger values overflow the cutoff arithmetic and stop retention entirely.</summary>
    public const int MaxDays = 3650;

    /// <summary>1 GiB, the smallest cap the Settings page offers. A tiny cap would evict every recording.</summary>
    public const long MinStorageBytes = 1L << 30;

    public static int ClampDays(int days) => Math.Clamp(days, MinDays, MaxDays);
}
