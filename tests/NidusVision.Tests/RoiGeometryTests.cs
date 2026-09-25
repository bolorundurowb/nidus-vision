using NidusVision.Core.Cameras;
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

    [Fact]
    public void RoiJsonParsesNormalizedPoints()
    {
        var points = RoiJson.Parse("""[{"x":0.1,"y":0.2},{"x":0.3,"y":0.4}]""");
        points.Must().HaveCount(2);
        points[0].X.Must().Be(0.1);
        points[1].Y.Must().Be(0.4);
    }

    [Fact]
    public void StreamDimensionsParseUnicodeSeparator()
    {
        StreamDimensions.TryParse("1920×1080", out var width, out var height).Must().BeTrue();
        width.Must().Be(1920);
        height.Must().Be(1080);
    }
}
