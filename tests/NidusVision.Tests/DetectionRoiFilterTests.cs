using NidusVision.Core.Inference;
using NidusVision.Web.Inference;

namespace NidusVision.Tests;

public sealed class DetectionRoiFilterTests
{
    [Fact]
    public void FilterRoiKeepsBoxesWhoseCenterIsInsideThePolygon()
    {
        var roi = """[{"x":0.4,"y":0.4},{"x":0.6,"y":0.4},{"x":0.6,"y":0.6},{"x":0.4,"y":0.6}]""";
        var inside = new BoundingBox(45, 45, 10, 10, 0.9f);
        var outside = new BoundingBox(1, 1, 4, 4, 0.9f);

        var kept = DetectionHostedService.FilterRoi([inside, outside], roi, 100, 100);

        kept.Must().HaveCount(1);
        kept[0].X.Must().Be(45);
    }

    [Fact]
    public void FilterRoiAllowsAllBoxesWhenPolygonIsEmpty()
    {
        var box = new BoundingBox(1, 1, 4, 4, 0.9f);
        DetectionHostedService.FilterRoi([box], null, 100, 100).Must().HaveCount(1);
    }
}
