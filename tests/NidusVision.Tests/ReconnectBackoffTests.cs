using NidusVision.Streaming;

namespace NidusVision.Tests;

public sealed class ReconnectBackoffTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(10, 60)]
    public void DelayForAttemptReturnsExponentialDelayCappedAtOneMinute(int attempt, double expectedSeconds)
    {
        // Arrange
        var backoff = new ReconnectBackoff(TimeProvider.System);

        // Act
        var delay = backoff.DelayForAttempt(attempt);

        // Assert
        delay.TotalSeconds.Must().Be(expectedSeconds);
    }
}
