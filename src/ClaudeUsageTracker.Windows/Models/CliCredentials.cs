namespace ClaudeUsageTracker.Windows.Models;

/// <summary>
/// Parsed from Claude Code CLI's ~/.claude/.credentials.json (claudeAiOauth object). Used as a
/// fallback usage-fetch credential when no manual session key is configured.
/// </summary>
public sealed record CliCredentials(string AccessToken, DateTimeOffset? ExpiresAt, string? SubscriptionType)
{
    public bool IsExpired => ExpiresAt is { } expiry && DateTimeOffset.Now > expiry;
}
