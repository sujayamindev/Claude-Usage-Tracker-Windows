using ClaudeUsageTracker.Windows.Models;
using ClaudeUsageTracker.Windows.Services;

namespace ClaudeUsageTracker.Windows.Tests;

public class StatuslineCacheTests
{
    private static string TempCachePath() =>
        Path.Combine(Path.GetTempPath(), $"claude-statusline-cache-test-{Guid.NewGuid():N}.json");

    [Fact]
    public void WriteThenTryRead_RoundTripsWithinMaxAge()
    {
        var path = TempCachePath();
        try
        {
            var cache = new StatuslineCache(path);
            var usage = new ClaudeUsage
            {
                SessionPercentage = 42,
                SessionResetTime = DateTimeOffset.Now.AddHours(2),
                WeeklyPercentage = 61,
                WeeklyResetTime = DateTimeOffset.Now.AddDays(3),
                LastUpdated = DateTimeOffset.Now
            };

            cache.Write(usage);
            var entry = cache.TryRead(TimeSpan.FromSeconds(90));

            Assert.NotNull(entry);
            Assert.Equal(42, entry!.SessionPercentage);
            Assert.Equal(61, entry.WeeklyPercentage);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryRead_ReturnsNullWhenFileDoesNotExist()
    {
        var cache = new StatuslineCache(TempCachePath());

        Assert.Null(cache.TryRead(TimeSpan.FromSeconds(90)));
    }

    [Fact]
    public void TryRead_ReturnsNullWhenEntryIsOlderThanMaxAge()
    {
        var path = TempCachePath();
        try
        {
            var staleEntry = new StatuslineCacheEntry(
                SessionPercentage: 10,
                SessionResetTime: DateTimeOffset.Now.AddHours(1),
                WeeklyPercentage: 20,
                WeeklyResetTime: DateTimeOffset.Now.AddDays(1),
                WrittenAt: DateTimeOffset.Now.AddSeconds(-120));
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(staleEntry));

            var cache = new StatuslineCache(path);

            Assert.Null(cache.TryRead(TimeSpan.FromSeconds(90)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryRead_ReturnsNullOnCorruptFile()
    {
        var path = TempCachePath();
        try
        {
            File.WriteAllText(path, "{ not valid json");
            var cache = new StatuslineCache(path);

            Assert.Null(cache.TryRead(TimeSpan.FromSeconds(90)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Write_CreatesParentDirectoryIfMissing()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"claude-statusline-dir-test-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, ".statusline-usage-cache");
        try
        {
            var cache = new StatuslineCache(path);
            var usage = new ClaudeUsage
            {
                SessionPercentage = 5,
                SessionResetTime = DateTimeOffset.Now.AddHours(1),
                WeeklyPercentage = 5,
                WeeklyResetTime = DateTimeOffset.Now.AddDays(1),
                LastUpdated = DateTimeOffset.Now
            };

            cache.Write(usage);

            Assert.True(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
