using ClaudeUsageTracker.Windows.Models;
using ClaudeUsageTracker.Windows.Services;

namespace ClaudeUsageTracker.Windows.Tests;

public class UsageStatusCalculatorTests
{
    [Theory]
    [InlineData(0, UsageStatusLevel.Safe)]
    [InlineData(25, UsageStatusLevel.Safe)]
    [InlineData(49.9, UsageStatusLevel.Safe)]
    [InlineData(50, UsageStatusLevel.Moderate)]
    [InlineData(65, UsageStatusLevel.Moderate)]
    [InlineData(79.9, UsageStatusLevel.Moderate)]
    [InlineData(80, UsageStatusLevel.Critical)]
    [InlineData(95, UsageStatusLevel.Critical)]
    [InlineData(100, UsageStatusLevel.Critical)]
    public void CalculateStatus_UsesUsedPercentageThresholds(double usedPercentage, UsageStatusLevel expected)
    {
        Assert.Equal(expected, UsageStatusCalculator.CalculateStatus(usedPercentage));
    }
}
