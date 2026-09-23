namespace NidusVision.Core.Models;

public enum CameraStatus
{
    Offline = 0,
    Online = 1,
    Recording = 2
}

public enum RtspTransport
{
    Tcp = 0,
    Udp = 1
}

public sealed class Camera
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Name { get; set; }
    public string Location { get; set; } = "Unassigned";
    public bool Enabled { get; set; } = true;
    public required string MainRtspUrl { get; set; }
    public string? SubRtspUrl { get; set; }
    public string? Username { get; set; }
    public string? PasswordProtected { get; set; }
    public RtspTransport Transport { get; set; } = RtspTransport.Tcp;
    public CameraStatus Status { get; set; } = CameraStatus.Offline;
    public string? RoiJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<RecordingSegment> Segments { get; set; } = [];
    public ICollection<DetectionEvent> Detections { get; set; } = [];
}

public sealed class RecordingSegment
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CameraId { get; set; }
    public Camera Camera { get; set; } = null!;
    public required string Path { get; set; }
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset EndUtc { get; set; }
    public string Codec { get; set; } = "h264";
    public bool HasHuman { get; set; }
    public long ByteSize { get; set; }
}

public sealed class DetectionEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CameraId { get; set; }
    public Camera Camera { get; set; } = null!;
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset EndUtc { get; set; }
    public float Confidence { get; set; }
    public string? BoundingBoxJson { get; set; }
    public string? ThumbnailPath { get; set; }
    public string? SegmentIdsJson { get; set; }
}

public sealed class AppSettings
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public int GeneralRetentionDays { get; set; } = 30;
    public int DetectionRetentionDays { get; set; } = 90;
    public long? MaxStorageBytes { get; set; }
    public bool InferenceEnabled { get; set; } = true;
    public float SampleFps { get; set; } = 1f;
    public float ConfidenceThreshold { get; set; } = 0.6f;
}

public sealed class LocalUser
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string PasswordHash { get; set; }
}
