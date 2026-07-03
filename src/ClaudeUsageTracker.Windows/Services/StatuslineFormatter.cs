using ClaudeUsageTracker.Windows.Models;

namespace ClaudeUsageTracker.Windows.Services;

/// <summary>
/// Renders the one-line statusline text printed by the --statusline CLI mode. The coding-context
/// half (from Claude Code's stdin JSON) and the usage half (from the cache file) degrade
/// independently — either can be missing without preventing the other from rendering.
/// </summary>
public static class StatuslineFormatter
{
    private const string AnsiReset = "[0m";

    public static string Format(StatuslineInput? input, StatuslineCacheEntry? cacheEntry, bool useAnsiColor = true)
    {
        var contextPart = FormatContext(input);
        var usagePart = FormatUsage(cacheEntry, useAnsiColor);

        return contextPart is null ? usagePart : $"{contextPart} │ {usagePart}";
    }

    private static string? FormatContext(StatuslineInput? input)
    {
        if (input is null)
            return null;

        var pieces = new List<string>();

        if (input.CurrentDirectory is { Length: > 0 } dir)
            pieces.Add(ShortenHomePath(dir));

        if (input.ModelDisplayName is { Length: > 0 } model)
            pieces.Add(model);

        if (input.ContextWindowPercentage is { } contextPct)
            pieces.Add($"{contextPct:0}% context");

        return pieces.Count > 0 ? string.Join(" · ", pieces) : null;
    }

    private static string FormatUsage(StatuslineCacheEntry? cacheEntry, bool useAnsiColor)
    {
        if (cacheEntry is null)
            return "Claude: tray app not running";

        var status = (UsageStatusLevel)Math.Max(
            (int)UsageStatusCalculator.CalculateStatus(cacheEntry.SessionPercentage),
            (int)UsageStatusCalculator.CalculateStatus(cacheEntry.WeeklyPercentage));

        var text = $"Claude: {cacheEntry.SessionPercentage:0}% session (resets {FormatRelativeTime(cacheEntry.SessionResetTime)}) " +
                   $"· {cacheEntry.WeeklyPercentage:0}% weekly (resets {FormatRelativeTime(cacheEntry.WeeklyResetTime)})";

        if (!useAnsiColor)
            return text;

        return $"{AnsiColor(status)}{text}{AnsiReset}";
    }

    private static string ShortenHomePath(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return path.StartsWith(home, StringComparison.OrdinalIgnoreCase)
            ? "~" + path[home.Length..].Replace('\\', '/')
            : path.Replace('\\', '/');
    }

    private static string FormatRelativeTime(DateTimeOffset resetTime)
    {
        var remaining = resetTime - DateTimeOffset.Now;
        if (remaining <= TimeSpan.Zero)
            return "now";

        return remaining.TotalHours < 24
            ? $"{(int)remaining.TotalHours}h{remaining.Minutes}m"
            : $"{(int)remaining.TotalDays}d";
    }

    private static string AnsiColor(UsageStatusLevel level) => level switch
    {
        UsageStatusLevel.Safe => "[32m",
        UsageStatusLevel.Moderate => "[33m",
        UsageStatusLevel.Critical => "[31m",
        _ => string.Empty
    };
}
