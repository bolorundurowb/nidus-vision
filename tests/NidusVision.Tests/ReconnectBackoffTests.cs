using NidusVision.Streaming;

namespace NidusVision.Tests;

public sealed class ReconnectBackoffTests
{
    [Fact]
    public void Delay_grows_then_caps()
    {
        var backoff = new ReconnectBackoff(TimeProvider.System);
        backoff.DelayForAttempt(0).TotalSeconds.Must().Be(1);
        backoff.DelayForAttempt(1).TotalSeconds.Must().Be(2);
        backoff.DelayForAttempt(10).TotalSeconds.Must().Be(60);
    }
}
