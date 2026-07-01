using ClaudeUsageTracker.Windows.Models;

namespace ClaudeUsageTracker.Windows.Services;

/// <summary>
/// Ported from the macOS app's UsageStatusCalculator (used-percentage mode only for MVP).
/// </summary>
public static class UsageStatusCalculator
{
    public static UsageStatusLevel CalculateStatus(double usedPercentage)
    {
        return usedPercentage switch
        {
            < 50 => UsageStatusLevel.Safe,
            < 80 => UsageStatusLevel.Moderate,
            _ => UsageStatusLevel.Critical
        };
    }
}
