namespace ClaudeUsageTracker.Windows.Models;

public sealed record StatuslineCacheEntry(
    double SessionPercentage,
    DateTimeOffset SessionResetTime,
    double WeeklyPercentage,
    DateTimeOffset WeeklyResetTime,
    DateTimeOffset WrittenAt);
