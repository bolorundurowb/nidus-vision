namespace NidusVision.Core.Roi;

public readonly record struct Point(double X, double Y);

public static class RoiGeometry
{
    public static bool Contains(IReadOnlyList<Point> polygon, Point point)
    {
        if (polygon.Count < 3)
        {
            return true;
        }

        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];
            var intersect = ((pi.Y > point.Y) != (pj.Y > point.Y))
                && (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y + double.Epsilon) + pi.X);
            if (intersect)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    public static bool BoxIntersectsRoi(IReadOnlyList<Point> polygon, double x, double y, double w, double h) =>
        Contains(polygon, new Point(x + w / 2, y + h / 2));
}
