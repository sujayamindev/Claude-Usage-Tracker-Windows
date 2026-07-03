using System.IO;
using System.Text.Json;
using ClaudeUsageTracker.Windows.Models;

namespace ClaudeUsageTracker.Windows.Services;

/// <summary>
/// Writes/reads the small usage snapshot the --statusline CLI mode reads on every Claude Code
/// prompt render. Written by UsagePollingService on each successful poll while the statusline
/// feature is enabled; read fresh (no in-memory caching) so it always reflects the latest poll.
/// </summary>
public sealed class StatuslineCache(string? cacheFilePath = null)
{
    private readonly string _cacheFilePath = cacheFilePath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", ".statusline-usage-cache");

    public void Write(ClaudeUsage usage)
    {
        var entry = new StatuslineCacheEntry(
            usage.EffectiveSessionPercentage,
            usage.SessionResetTime,
            usage.WeeklyPercentage,
            usage.WeeklyResetTime,
            DateTimeOffset.Now);

        var directory = Path.GetDirectoryName(_cacheFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(_cacheFilePath, JsonSerializer.Serialize(entry));
    }

    public StatuslineCacheEntry? TryRead(TimeSpan maxAge)
    {
        if (!File.Exists(_cacheFilePath))
            return null;

        StatuslineCacheEntry? entry;
        try
        {
            entry = JsonSerializer.Deserialize<StatuslineCacheEntry>(File.ReadAllText(_cacheFilePath));
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        if (entry is null)
            return null;

        return DateTimeOffset.Now - entry.WrittenAt > maxAge ? null : entry;
    }
}
