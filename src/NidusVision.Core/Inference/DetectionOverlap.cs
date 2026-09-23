namespace NidusVision.Core.Inference;

public static class DetectionOverlap
{
    public static bool Overlaps(DateTimeOffset segmentStart, DateTimeOffset segmentEnd, DateTimeOffset eventStart, DateTimeOffset eventEnd) =>
        segmentStart < eventEnd && eventStart < segmentEnd;
}

public readonly record struct BoundingBox(float X, float Y, float Width, float Height, float Confidence);

public static class NonMaxSuppression
{
    public static IReadOnlyList<BoundingBox> Filter(IReadOnlyList<BoundingBox> boxes, float iouThreshold)
    {
        var ordered = boxes.OrderByDescending(b => b.Confidence).ToList();
        var kept = new List<BoundingBox>();
        while (ordered.Count > 0)
        {
            var current = ordered[0];
            kept.Add(current);
            ordered.RemoveAt(0);
            ordered.RemoveAll(b => IoU(current, b) > iouThreshold);
        }

        return kept;
    }

    internal static float IoU(BoundingBox a, BoundingBox b)
    {
        var x1 = Math.Max(a.X, b.X);
        var y1 = Math.Max(a.Y, b.Y);
        var x2 = Math.Min(a.X + a.Width, b.X + b.Width);
        var y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);
        var inter = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        var union = a.Width * a.Height + b.Width * b.Height - inter;
        return union <= 0 ? 0 : inter / union;
    }
}
