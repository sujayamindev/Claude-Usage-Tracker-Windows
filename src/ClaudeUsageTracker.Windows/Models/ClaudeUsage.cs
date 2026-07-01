namespace ClaudeUsageTracker.Windows.Models;

/// <summary>
/// Usage statistics for the 5-hour session window and 7-day weekly window.
/// Ported from the macOS app's ClaudeUsage model (session/weekly fields only for MVP).
/// </summary>
public sealed class ClaudeUsage
{
    public double SessionPercentage { get; init; }
    public DateTimeOffset SessionResetTime { get; init; }

    public double WeeklyPercentage { get; init; }
    public DateTimeOffset WeeklyResetTime { get; init; }

    public double OpusWeeklyPercentage { get; init; }

    public double SonnetWeeklyPercentage { get; init; }
    public DateTimeOffset? SonnetWeeklyResetTime { get; init; }

    public DateTimeOffset LastUpdated { get; init; }

    /// <summary>0% once the 5-hour session window has expired, otherwise the raw percentage.</summary>
    public double EffectiveSessionPercentage =>
        SessionResetTime < DateTimeOffset.Now ? 0.0 : SessionPercentage;

    public static ClaudeUsage Empty { get; } = new()
    {
        SessionPercentage = 0,
        SessionResetTime = DateTimeOffset.Now.AddHours(5),
        WeeklyPercentage = 0,
        WeeklyResetTime = DateTimeOffset.Now.AddDays(7),
        OpusWeeklyPercentage = 0,
        SonnetWeeklyPercentage = 0,
        SonnetWeeklyResetTime = null,
        LastUpdated = DateTimeOffset.Now
    };
}
