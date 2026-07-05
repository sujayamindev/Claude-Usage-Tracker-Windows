using ClaudeUsageTracker.Windows.Models;

namespace ClaudeUsageTracker.Windows.Tests;

public class PaceStatusTests
{
    // Boundary values for projected = used/elapsed:
    // projected = 0.25/0.5 = 0.50 → OnTrack boundary (< 0.50 = Comfortable)
    // projected = 0.375/0.5 = 0.75 → Warming boundary
    // projected = 0.45/0.5 = 0.90 → Pressing boundary
    // projected = 0.50/0.5 = 1.00 → Critical boundary
    // projected = 0.60/0.5 = 1.20 → Runaway boundary

    [Theory]
    [InlineData(20.0, 0.5, PaceStatus.Comfortable)]   // projected 0.40 < 0.50
    [InlineData(24.9, 0.5, PaceStatus.Comfortable)]   // projected 0.498 < 0.50
    [InlineData(25.0, 0.5, PaceStatus.OnTrack)]        // projected 0.50 = boundary
    [InlineData(37.4, 0.5, PaceStatus.OnTrack)]        // projected 0.748 < 0.75
    [InlineData(37.5, 0.5, PaceStatus.Warming)]        // projected 0.75 = boundary
    [InlineData(44.9, 0.5, PaceStatus.Warming)]        // projected 0.898 < 0.90
    [InlineData(45.0, 0.5, PaceStatus.Pressing)]       // projected 0.90 = boundary
    [InlineData(49.9, 0.5, PaceStatus.Pressing)]       // projected 0.998 < 1.00
    [InlineData(50.0, 0.5, PaceStatus.Critical)]       // projected 1.00 = boundary
    [InlineData(59.9, 0.5, PaceStatus.Critical)]       // projected 1.198 < 1.20
    [InlineData(60.0, 0.5, PaceStatus.Runaway)]        // projected 1.20 = boundary
    [InlineData(80.0, 0.5, PaceStatus.Runaway)]        // projected 1.60
    public void Calculate_ReturnsCorrectTier(double usedPct, double elapsed, PaceStatus expected)
    {
        var result = PaceStatusCalculator.Calculate(usedPct, elapsed);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Calculate_ReturnsComfortable_WhenUsageIsZero()
    {
        var result = PaceStatusCalculator.Calculate(0.0, 0.5);
        Assert.Equal(PaceStatus.Comfortable, result);
    }

    [Theory]
    [InlineData(0.0)]    // nothing elapsed
    [InlineData(0.02)]   // below 3% threshold
    [InlineData(0.029)]  // just under threshold
    public void Calculate_ReturnsNull_WhenElapsedBelowMinimum(double elapsed)
    {
        var result = PaceStatusCalculator.Calculate(50.0, elapsed);
        Assert.Null(result);
    }

    [Theory]
    [InlineData(1.0)]   // period exactly over
    [InlineData(1.1)]   // period past
    public void Calculate_ReturnsNull_WhenPeriodOver(double elapsed)
    {
        var result = PaceStatusCalculator.Calculate(50.0, elapsed);
        Assert.Null(result);
    }
}
