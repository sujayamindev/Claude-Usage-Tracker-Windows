using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ClaudeUsageTracker.Windows.Models;

namespace ClaudeUsageTracker.Windows.Services;

/// <summary>
/// Reads Claude Code CLI's OAuth credentials from ~/.claude/.credentials.json. This is the
/// cross-platform file the macOS app's ClaudeCodeSyncService also prefers over the system
/// keychain (it's "always complete, not subject to keychain truncation"). Always re-reads from
/// disk — nothing is cached — so it stays in sync with Claude Code's own token refresh, which
/// rewrites this file in place.
/// </summary>
public sealed class CliCredentialReader(string? credentialsFilePath = null)
{
    private readonly string _credentialsFilePath = credentialsFilePath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", ".credentials.json");

    public CliCredentials? TryRead()
    {
        if (!File.Exists(_credentialsFilePath))
            return null;

        string json;
        try
        {
            json = File.ReadAllText(_credentialsFilePath);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("claudeAiOauth", out var oauth) ||
                oauth.ValueKind != JsonValueKind.Object)
                return null;

            if (!oauth.TryGetProperty("accessToken", out var accessTokenProp) ||
                accessTokenProp.ValueKind != JsonValueKind.String)
                return null;

            var accessToken = accessTokenProp.GetString()!;

            DateTimeOffset? expiresAt = null;
            if (oauth.TryGetProperty("expiresAt", out var expiresAtProp) &&
                expiresAtProp.ValueKind == JsonValueKind.Number &&
                expiresAtProp.TryGetDouble(out var expiresAtValue))
            {
                // Claude Code CLI stores expiresAt in milliseconds since epoch. Values > 1e12 are
                // definitely milliseconds (year 2001+ in ms vs. year 33658 in seconds) — mirrors
                // the macOS app's ClaudeCodeSyncService.extractTokenExpiry heuristic exactly.
                var epochSeconds = expiresAtValue > 1e12 ? expiresAtValue / 1000.0 : expiresAtValue;
                expiresAt = DateTimeOffset.FromUnixTimeMilliseconds((long)(epochSeconds * 1000));
            }

            string? subscriptionType = oauth.TryGetProperty("subscriptionType", out var subProp) &&
                subProp.ValueKind == JsonValueKind.String
                ? subProp.GetString()
                : null;

            return new CliCredentials(accessToken, expiresAt, subscriptionType);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Retries TryRead() a bounded number of times with a delay in between, to smooth over the
    /// race where Claude Code CLI's credentials file isn't valid yet at the exact instant of a
    /// one-time startup check (e.g. CLI login/token refresh completing a moment after this app
    /// launched). Returns the first non-expired result, or null if every attempt comes up empty.
    /// </summary>
    public async Task<CliCredentials?> TryReadWithRetryAsync(int maxAttempts = 3, TimeSpan? delayBetweenAttempts = null)
    {
        var delay = delayBetweenAttempts ?? TimeSpan.FromMilliseconds(500);
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (TryRead() is { IsExpired: false } credentials)
                return credentials;

            if (attempt < maxAttempts - 1)
                await Task.Delay(delay);
        }
        return null;
    }
}
