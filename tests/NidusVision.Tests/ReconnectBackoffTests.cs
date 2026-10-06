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

    [Fact]
    public void NextAttemptEscalatesWhenTheRunFailedBeforeStarting()
    {
        // Act
        var next = ReconnectBackoff.NextAttemptAfter(3, null);

        // Assert
        next.Must().Be(4);
    }

    [Fact]
    public void NextAttemptEscalatesAfterAShortRun()
    {
        // Act
        var next = ReconnectBackoff.NextAttemptAfter(5, TimeSpan.FromSeconds(10));

        // Assert
        next.Must().Be(6);
    }

    [Fact]
    public void NextAttemptResetsAfterAStableRun()
    {
        // Act
        var next = ReconnectBackoff.NextAttemptAfter(9, ReconnectBackoff.StableRunThreshold);

        // Assert
        next.Must().Be(1);
    }

    [Fact]
    public void StableRunMeasuredWithTimeProviderResetsTheBackoff()
    {
        // Arrange
        var time = new SteppingTimeProvider();
        var backoff = new ReconnectBackoff(time);
        var started = backoff.StartRun();
        time.Advance(TimeSpan.FromHours(2));

        // Act
        var next = backoff.NextAttempt(12, started);

        // Assert
        next.Must().Be(1);
        backoff.DelayForAttempt(next).TotalSeconds.Must().Be(2);
    }

    private sealed class SteppingTimeProvider : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan by) => _ticks += by.Ticks;
    }
}
