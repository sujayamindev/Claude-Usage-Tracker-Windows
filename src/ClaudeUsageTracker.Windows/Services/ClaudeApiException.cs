namespace ClaudeUsageTracker.Windows.Services;

public sealed class ClaudeApiException(string message, bool isUnauthorized = false) : Exception(message)
{
    /// <summary>True when the API rejected the session key (401/403) — caller should re-prompt for setup.</summary>
    public bool IsUnauthorized { get; } = isUnauthorized;
}
