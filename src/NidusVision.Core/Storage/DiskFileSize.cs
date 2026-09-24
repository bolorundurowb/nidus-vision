namespace NidusVision.Core.Storage;

public static class DiskFileSize
{
    public static long Of(string path, long indexedBytes)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? info.Length : indexedBytes;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return indexedBytes;
        }
    }
}
