using NidusVision.Core.Events;

namespace NidusVision.Tests;

public sealed class EventSearchFilterTests
{
    [Fact]
    public void Matches_filters_by_query_camera_and_confidence()
    {
        var camera = Guid.CreateVersion7();
        Assert.True(EventSearchFilter.Matches("Person Front Yard", "front", camera, camera, 0.9f, 0.6f));
        Assert.False(EventSearchFilter.Matches("Person Front Yard", "garage", camera, camera, 0.9f, 0.6f));
        Assert.False(EventSearchFilter.Matches("Person Front Yard", null, camera, Guid.CreateVersion7(), 0.9f, 0.6f));
        Assert.False(EventSearchFilter.Matches("Person Front Yard", null, camera, camera, 0.4f, 0.6f));
    }
}
