using ClaudeUsageTracker.Windows.Models;
using ClaudeUsageTracker.Windows.Services;

namespace ClaudeUsageTracker.Windows.Tests;

public class StatuslineFormatterTests
{
    private static StatuslineCacheEntry SafeCacheEntry() => new(
        SessionPercentage: 10,
        SessionResetTime: DateTimeOffset.Now.AddHours(2).AddMinutes(14),
        WeeklyPercentage: 20,
        WeeklyResetTime: DateTimeOffset.Now.AddDays(3),
        WrittenAt: DateTimeOffset.Now);

    [Fact]
    public void Format_WithInputAndCache_CombinesBothHalves()
    {
        var input = new StatuslineInput("C:\\Users\\test\\my-project", "Sonnet 4.5", 12);
        var result = StatuslineFormatter.Format(input, SafeCacheEntry(), useAnsiColor: false);

        Assert.Contains("my-project", result);
        Assert.Contains("Sonnet 4.5", result);
        Assert.Contains("12% context", result);
        Assert.Contains("10% session", result);
        Assert.Contains("20% weekly", result);
    }

    [Fact]
    public void Format_WithNullInput_OmitsContextHalf()
    {
        var result = StatuslineFormatter.Format(null, SafeCacheEntry(), useAnsiColor: false);

        Assert.DoesNotContain("context", result);
        Assert.Contains("10% session", result);
    }

    [Fact]
    public void Format_WithNullCache_ShowsFallbackUsageText()
    {
        var input = new StatuslineInput("C:\\Users\\test\\my-project", "Sonnet 4.5", 12);
        var result = StatuslineFormatter.Format(input, null, useAnsiColor: false);

        Assert.Contains("my-project", result);
        Assert.Contains("tray app not running", result);
    }

    [Fact]
    public void Format_WithNullInputAndNullCache_ShowsOnlyFallbackText()
    {
        var result = StatuslineFormatter.Format(null, null, useAnsiColor: false);

        Assert.Equal("Claude: tray app not running", result);
    }

    [Fact]
    public void Format_UsesAnsiColorByDefault()
    {
        var result = StatuslineFormatter.Format(null, SafeCacheEntry());

        Assert.Contains('\x1B', result);
    }

    [Fact]
    public void Format_OmitsAnsiCodesWhenDisabled()
    {
        var result = StatuslineFormatter.Format(null, SafeCacheEntry(), useAnsiColor: false);

        Assert.DoesNotContain('\x1B', result);
    }

    [Fact]
    public void Format_ShortensHomeDirectoryPrefix()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var input = new StatuslineInput(Path.Combine(home, "my-project"), null, null);

        var result = StatuslineFormatter.Format(input, null, useAnsiColor: false);

        Assert.Contains("~/my-project", result);
    }
}
