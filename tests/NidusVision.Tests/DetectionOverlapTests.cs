using NidusVision.Core.Inference;

namespace NidusVision.Tests;

public sealed class DetectionOverlapTests
{
    [Theory]
    [InlineData(9, 12, true)]
    [InlineData(11, 12, false)]
    public void OverlapsReturnsExpectedResultForCandidateRange(int candidateStartMinute, int candidateEndMinute, bool expected)
    {
        // Arrange
        var start = DateTimeOffset.Parse("2026-09-23T14:00:00Z");

        // Act
        var overlaps = DetectionOverlap.Overlaps(
            start,
            start.AddMinutes(10),
            start.AddMinutes(candidateStartMinute),
            start.AddMinutes(candidateEndMinute));

        // Assert
        overlaps.Must().Be(expected);
    }

    [Fact]
    public void NmsDropsHeavyOverlap()
    {
        // Arrange and act
        var kept = NonMaxSuppression.Filter(
        [
            new BoundingBox(0, 0, 10, 10, 0.9f),
            new BoundingBox(1, 1, 10, 10, 0.8f),
            new BoundingBox(50, 50, 5, 5, 0.7f),
        ], 0.3f);

        // Assert
        kept.Must().HaveCount(2);
    }
}
