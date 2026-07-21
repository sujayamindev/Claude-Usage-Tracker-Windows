namespace ClaudeUsageTracker.Windows.Services;

/// <summary>
/// Detects session/weekly resets by comparing consecutive polls' reset times, ported from
/// MenuBarManager's checkAndRecordSessionReset/checkAndRecordWeeklyReset in the macOS app.
/// Normalizes to the minute and compares with != (not >) so it tolerates clock changes and
/// backward time jumps. One instance is scoped to a single profile — callers must call Reset()
/// when the active profile changes, so a profile switch never fires a false "reset detected".
/// </summary>
public sealed class ResetDetector
{
    private DateTimeOffset? _lastKnownSessionResetTime;
    private DateTimeOffset? _lastKnownWeeklyResetTime;

    public void Reset()
    {
        _lastKnownSessionResetTime = null;
        _lastKnownWeeklyResetTime = null;
    }

    /// <summary>True if a session reset was just detected (caller should record a snapshot of the previous usage).</summary>
    public bool CheckSessionReset(DateTimeOffset newSessionResetTime)
    {
        var normalized = NormalizeToMinute(newSessionResetTime);

        if (_lastKnownSessionResetTime is not { } lastKnown)
        {
            _lastKnownSessionResetTime = normalized;
            return false;
        }

        var resetDetected = normalized != NormalizeToMinute(lastKnown);
        _lastKnownSessionResetTime = normalized;
        return resetDetected;
    }

    /// <summary>True if a weekly reset was just detected (caller should record a snapshot of the previous usage).</summary>
    public bool CheckWeeklyReset(DateTimeOffset newWeeklyResetTime)
    {
        var normalized = NormalizeToMinute(newWeeklyResetTime);

        if (_lastKnownWeeklyResetTime is not { } lastKnown)
        {
            _lastKnownWeeklyResetTime = normalized;
            return false;
        }

        var resetDetected = normalized != NormalizeToMinute(lastKnown);
        _lastKnownWeeklyResetTime = normalized;
        return resetDetected;
    }

    private static DateTimeOffset NormalizeToMinute(DateTimeOffset time) =>
        new(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0, time.Offset);
}
