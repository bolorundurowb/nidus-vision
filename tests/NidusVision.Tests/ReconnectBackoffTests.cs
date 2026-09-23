using NidusVision.Streaming;

namespace NidusVision.Tests;

public sealed class ReconnectBackoffTests
{
    [Fact]
    public void Delay_grows_then_caps()
    {
        var backoff = new ReconnectBackoff(TimeProvider.System);
        Assert.Equal(1, backoff.DelayForAttempt(0).TotalSeconds);
        Assert.Equal(2, backoff.DelayForAttempt(1).TotalSeconds);
        Assert.Equal(60, backoff.DelayForAttempt(10).TotalSeconds);
    }
}
