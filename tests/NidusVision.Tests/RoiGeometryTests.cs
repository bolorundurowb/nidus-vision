using NidusVision.Core.Roi;

namespace NidusVision.Tests;

public sealed class RoiGeometryTests
{
    [Theory]
    [InlineData(5, 5, true)]
    [InlineData(20, 20, false)]
    public void ContainsReturnsExpectedResultForPoint(double x, double y, bool expected)
    {
        // Arrange
        Point[] square = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)];

        // Act
        var contains = RoiGeometry.Contains(square, new Point(x, y));

        // Assert
        contains.Must().Be(expected);
    }

    [Fact]
    public void ContainsWithEmptyRegionAllowsEveryPoint()
    {
        var contains = RoiGeometry.Contains([], new Point(99, 99));

        contains.Must().BeTrue();
    }
}
