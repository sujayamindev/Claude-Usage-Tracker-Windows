using ClaudeUsageTracker.Windows.Models;
using ClaudeUsageTracker.Windows.Services;

namespace ClaudeUsageTracker.Windows.Tests;

public class CostLedgerServiceTests
{
    private static string TempLedgerPath() =>
        Path.Combine(Path.GetTempPath(), $"cost-ledger-test-{Guid.NewGuid():N}.ndjson");

    [Fact]
    public void LoadAndCompact_WhenLedgerFileMissing_ReturnsEmptyList()
    {
        var service = new CostLedgerService(TempLedgerPath());

        var entries = service.LoadAndCompact();

        Assert.Empty(entries);
    }

    [Fact]
    public void LoadAndCompact_WithSingleValidLine_ReturnsOneEntry()
    {
        var path = TempLedgerPath();
        try
        {
            File.WriteAllText(path, """{"sessionId":"session-1","costUsd":1.42,"timestamp":"2026-07-24T18:03:11Z"}""" + "\n");
            var service = new CostLedgerService(path);

            var entries = service.LoadAndCompact();

            var entry = Assert.Single(entries);
            Assert.Equal("session-1", entry.SessionId);
            Assert.Equal(1.42m, entry.CostUsd);
            Assert.Equal(DateTimeOffset.Parse("2026-07-24T18:03:11Z"), entry.Timestamp);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadAndCompact_WithSameSessionMultipleTimes_KeepsMaxCost()
    {
        var path = TempLedgerPath();
        try
        {
            File.WriteAllLines(path,
            [
                """{"sessionId":"session-1","costUsd":0.50,"timestamp":"2026-07-24T18:00:00Z"}""",
                """{"sessionId":"session-1","costUsd":1.75,"timestamp":"2026-07-24T18:05:00Z"}""",
                """{"sessionId":"session-1","costUsd":1.20,"timestamp":"2026-07-24T18:10:00Z"}"""
            ]);
            var service = new CostLedgerService(path);

            var entries = service.LoadAndCompact();

            var entry = Assert.Single(entries);
            Assert.Equal(1.75m, entry.CostUsd);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadAndCompact_WithMalformedLineAmongValidLines_SkipsMalformedLine()
    {
        var path = TempLedgerPath();
        try
        {
            File.WriteAllLines(path,
            [
                """{"sessionId":"session-1","costUsd":1.00,"timestamp":"2026-07-24T18:00:00Z"}""",
                "not valid json at all",
                """{"sessionId":"session-2","costUsd":2.00,"timestamp":"2026-07-24T19:00:00Z"}"""
            ]);
            var service = new CostLedgerService(path);

            var entries = service.LoadAndCompact();

            Assert.Equal(2, entries.Count);
            Assert.Contains(entries, e => e.SessionId == "session-1" && e.CostUsd == 1.00m);
            Assert.Contains(entries, e => e.SessionId == "session-2" && e.CostUsd == 2.00m);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadAndCompact_RewritesFileWithDedupedRowsOnly()
    {
        var path = TempLedgerPath();
        try
        {
            File.WriteAllLines(path,
            [
                """{"sessionId":"session-1","costUsd":0.50,"timestamp":"2026-07-24T18:00:00Z"}""",
                """{"sessionId":"session-1","costUsd":1.75,"timestamp":"2026-07-24T18:05:00Z"}""",
                """{"sessionId":"session-2","costUsd":2.00,"timestamp":"2026-07-24T19:00:00Z"}"""
            ]);
            var service = new CostLedgerService(path);
            service.LoadAndCompact();

            var linesOnDisk = File.ReadAllLines(path);

            Assert.Equal(2, linesOnDisk.Length);
            var reloaded = new CostLedgerService(path).LoadAndCompact();
            Assert.Equal(2, reloaded.Count);
            Assert.Contains(reloaded, e => e.SessionId == "session-1" && e.CostUsd == 1.75m);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GetAllTimeTotal_WithNoEntries_ReturnsZero()
    {
        var service = new CostLedgerService(TempLedgerPath());

        Assert.Equal(0m, service.GetAllTimeTotal([]));
    }

    [Fact]
    public void GetAllTimeTotal_SumsAllEntries()
    {
        var service = new CostLedgerService(TempLedgerPath());
        var entries = new List<CostLedgerEntry>
        {
            new("session-1", 1.50m, DateTimeOffset.Parse("2026-07-24T18:00:00Z")),
            new("session-2", 2.25m, DateTimeOffset.Parse("2026-07-24T19:00:00Z"))
        };

        Assert.Equal(3.75m, service.GetAllTimeTotal(entries));
    }

    [Fact]
    public void GetDailyTotals_GroupsBySameCalendarDateAndSortsNewestFirst()
    {
        var service = new CostLedgerService(TempLedgerPath());
        var entries = new List<CostLedgerEntry>
        {
            new("session-1", 1.00m, new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero)),
            new("session-2", 2.00m, new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero)),
            new("session-3", 0.50m, new DateTimeOffset(2026, 7, 24, 12, 5, 0, TimeSpan.Zero))
        };

        var dailyTotals = service.GetDailyTotals(entries);

        Assert.Equal(2, dailyTotals.Count);
        Assert.Equal(DateOnly.FromDateTime(new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero).LocalDateTime), dailyTotals[0].Date);
        Assert.Equal(2.50m, dailyTotals[0].Total);
        Assert.Equal(DateOnly.FromDateTime(new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero).LocalDateTime), dailyTotals[1].Date);
        Assert.Equal(1.00m, dailyTotals[1].Total);
    }
}
