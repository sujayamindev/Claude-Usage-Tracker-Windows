namespace ClaudeUsageTracker.Windows.Models;

public enum PaceStatus
{
    Comfortable = 0,
    OnTrack     = 1,
    Warming     = 2,
    Pressing    = 3,
    Critical    = 4,
    Runaway     = 5
}

/// <summary>
/// Ported from the macOS app's PaceStatus.swift.
/// Projects end-of-period usage from current consumption rate to determine urgency.
/// </summary>
public static class PaceStatusCalculator
{
    /// <summary>
    /// Returns null when elapsedFraction is below 3% (insufficient data) or >= 100% (period over).
    /// Formula: projected = (usedPercentage / 100) / elapsedFraction
    /// </summary>
    public static PaceStatus? Calculate(double usedPercentage, double elapsedFraction)
    {
        if (elapsedFraction < 0.03 || elapsedFraction >= 1.0)
            return null;

        if (usedPercentage <= 0)
            return PaceStatus.Comfortable;

        var projected = (usedPercentage / 100.0) / elapsedFraction;

        return projected switch
        {
            < 0.50 => PaceStatus.Comfortable,
            < 0.75 => PaceStatus.OnTrack,
            < 0.90 => PaceStatus.Warming,
            < 1.00 => PaceStatus.Pressing,
            < 1.20 => PaceStatus.Critical,
            _      => PaceStatus.Runaway
        };
    }
}
