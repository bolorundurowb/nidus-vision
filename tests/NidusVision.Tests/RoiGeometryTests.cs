using NidusVision.Core.Roi;

namespace NidusVision.Tests;

public sealed class RoiGeometryTests
{
    [Fact]
    public void Square_contains_center_not_outside()
    {
        Point[] square = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)];
        Assert.True(RoiGeometry.Contains(square, new Point(5, 5)));
        Assert.False(RoiGeometry.Contains(square, new Point(20, 20)));
        Assert.True(RoiGeometry.Contains([], new Point(99, 99)));
    }
}
