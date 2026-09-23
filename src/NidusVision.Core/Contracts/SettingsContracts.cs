namespace NidusVision.Core.Contracts;

public sealed record SettingsResponse(
    int GeneralRetentionDays,
    int DetectionRetentionDays,
    long? MaxStorageBytes,
    bool InferenceEnabled,
    float SampleFps,
    float ConfidenceThreshold);

public sealed record SettingsWriteRequest(
    int GeneralRetentionDays,
    int DetectionRetentionDays,
    long? MaxStorageBytes,
    bool InferenceEnabled,
    float SampleFps,
    float ConfidenceThreshold);

public sealed record StorageMetrics(long UsedBytes, long TotalBytes, long GeneralBytes, long DetectionBytes, long DatabaseBytes);

public sealed record SystemMetricsResponse(
    double CpuPercent,
    long MemoryUsedBytes,
    long MemoryTotalBytes,
    TimeSpan Uptime,
    double? InferenceLatencyMs,
    StorageMetrics Storage,
    string Version);
