using ClaudeUsageTracker.Windows.Models;

namespace ClaudeUsageTracker.Windows.Services;

public readonly record struct ChartWindow(DateTimeOffset Start, DateTimeOffset End);

/// <summary>
/// Pure time-window math for the usage history chart's pan navigation, ported from
/// CombinedUsageChart's visibleRange/stepHours/canGoBack/canGoForward in the macOS app.
/// </summary>
public static class ChartWindowCalculator
{
    public static ChartWindow VisibleRange(ChartTimeScale scale, double timeOffsetHours, DateTimeOffset now)
    {
        var end = now.AddHours(timeOffsetHours);
        var start = end.AddHours(-(double)scale);
        return new ChartWindow(start, end);
    }

    public static double StepHours(ChartTimeScale scale) => (double)scale / 2.0;

    public static bool CanGoForward(double timeOffsetHours) => timeOffsetHours < 0;

    public static bool CanGoBack(DateTimeOffset? oldestSnapshotTimestamp, ChartWindow visibleRange) =>
        oldestSnapshotTimestamp is { } oldest && oldest < visibleRange.Start;
}
