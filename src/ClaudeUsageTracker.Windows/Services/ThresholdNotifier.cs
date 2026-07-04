using ClaudeUsageTracker.Windows.Models;

namespace ClaudeUsageTracker.Windows.Services;

public sealed record NotificationEvent(NotificationMetric Metric, int Percentage);

/// <summary>
/// Pure threshold-crossing logic, no UI/transport dependency (mirrors UsageStatusCalculator's
/// testability). Tracks which thresholds have already fired per metric so a poll sitting above a
/// threshold doesn't refire it; the fired-set for a metric clears when that metric's reset time
/// advances, so alerts "re-arm" for the next session/week.
/// </summary>
public sealed class ThresholdNotifier(NotificationSettingsStore settingsStore)
{
    private readonly HashSet<int> _firedSession = [];
    private readonly HashSet<int> _firedWeekly = [];
    private DateTimeOffset? _lastSessionResetTime;
    private DateTimeOffset? _lastWeeklyResetTime;

    public IReadOnlyList<NotificationEvent> Evaluate(ClaudeUsage usage)
    {
        NotificationSettings settings;
        try
        {
            settings = settingsStore.Load();
        }
        catch (NotificationSettingsException)
        {
            return [];
        }

        if (!settings.NotificationsEnabled)
            return [];

        // Truncate to seconds so two polls within the same second (e.g. in tests) don't
        // falsely appear to have advanced the reset time.
        var sessionResetSec = usage.SessionResetTime.ToUnixTimeSeconds();
        if (_lastSessionResetTime?.ToUnixTimeSeconds() != sessionResetSec)
        {
            _firedSession.Clear();
            _lastSessionResetTime = usage.SessionResetTime;
        }

        var weeklyResetSec = usage.WeeklyResetTime.ToUnixTimeSeconds();
        if (_lastWeeklyResetTime?.ToUnixTimeSeconds() != weeklyResetSec)
        {
            _firedWeekly.Clear();
            _lastWeeklyResetTime = usage.WeeklyResetTime;
        }

        List<NotificationEvent> events = [];
        events.AddRange(EvaluateMetric(NotificationMetric.Session, usage.EffectiveSessionPercentage, settings, _firedSession));
        events.AddRange(EvaluateMetric(NotificationMetric.Weekly, usage.WeeklyPercentage, settings, _firedWeekly));
        return events;
    }

    private static IEnumerable<NotificationEvent> EvaluateMetric(
        NotificationMetric metric, double percentage, NotificationSettings settings, HashSet<int> fired)
    {
        foreach (var threshold in settings.Thresholds
                     .Where(t => t.Metric == metric && t.Enabled)
                     .OrderBy(t => t.Percentage))
        {
            if (percentage >= threshold.Percentage && fired.Add(threshold.Percentage))
                yield return new NotificationEvent(metric, threshold.Percentage);
        }
    }
}
