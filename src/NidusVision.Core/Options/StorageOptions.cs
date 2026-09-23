namespace NidusVision.Core.Options;

public sealed class StorageOptions
{
    public string DataDirectory { get; set; } = "data";
    public string RecordingsDirectory { get; set; } = "recordings";
    public string DatabaseFileName { get; set; } = "nidus.db";
}
