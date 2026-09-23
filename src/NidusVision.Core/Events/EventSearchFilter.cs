namespace NidusVision.Core.Events;

public static class EventSearchFilter
{
    public static bool Matches(string haystack, string? query, Guid cameraId, Guid? cameraFilter, float confidence, float minConfidence)
    {
        if (cameraFilter is { } id && id != cameraId)
        {
            return false;
        }

        if (confidence < minConfidence)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return haystack.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
