using ClaudeUsageTracker.Windows.Models;
using ClaudeUsageTracker.Windows.Services;

namespace ClaudeUsageTracker.Windows.Tests;

public class ChartWindowCalculatorTests
{
    [Fact]
    public void VisibleRange_AtZeroOffset_EndsAtNow()
    {
        var now = new DateTimeOffset(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

        var range = ChartWindowCalculator.VisibleRange(ChartTimeScale.Hours24, 0, now);

        Assert.Equal(now, range.End);
        Assert.Equal(now.AddHours(-24), range.Start);
    }

    [Fact]
    public void VisibleRange_WithNegativeOffset_ShiftsWindowIntoThePast()
    {
        var now = new DateTimeOffset(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

        var range = ChartWindowCalculator.VisibleRange(ChartTimeScale.Hours24, -12, now);

        Assert.Equal(now.AddHours(-12), range.End);
        Assert.Equal(now.AddHours(-36), range.Start);
    }

    [Theory]
    [InlineData(ChartTimeScale.Hours5, 2.5)]
    [InlineData(ChartTimeScale.Hours24, 12)]
    [InlineData(ChartTimeScale.Days7, 84)]
    [InlineData(ChartTimeScale.Days30, 360)]
    public void StepHours_IsHalfTheWindow(ChartTimeScale scale, double expected)
    {
        Assert.Equal(expected, ChartWindowCalculator.StepHours(scale));
    }

    [Fact]
    public void CanGoForward_TrueOnlyWhenOffsetIsNegative()
    {
        Assert.True(ChartWindowCalculator.CanGoForward(-1));
        Assert.False(ChartWindowCalculator.CanGoForward(0));
    }

    [Fact]
    public void CanGoBack_TrueWhenOldestSnapshotPredatesWindowStart()
    {
        var now = new DateTimeOffset(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);
        var range = ChartWindowCalculator.VisibleRange(ChartTimeScale.Hours24, 0, now);

        Assert.True(ChartWindowCalculator.CanGoBack(range.Start.AddHours(-1), range));
        Assert.False(ChartWindowCalculator.CanGoBack(range.Start.AddHours(1), range));
        Assert.False(ChartWindowCalculator.CanGoBack(null, range));
    }
}
