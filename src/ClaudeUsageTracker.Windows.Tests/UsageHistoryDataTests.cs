using ClaudeUsageTracker.Windows.Models;

namespace ClaudeUsageTracker.Windows.Tests;

public class UsageHistoryDataTests
{
    [Fact]
    public void SessionSnapshots_FiltersToSessionResetTypeOnly()
    {
        var now = DateTimeOffset.UtcNow;
        var data = new UsageHistoryData
        {
            Snapshots =
            [
                new UsageSnapshot { Timestamp = now, ResetType = ResetType.SessionReset, SessionPercentage = 42, TriggeringResetTime = now },
                new UsageSnapshot { Timestamp = now, ResetType = ResetType.WeeklyReset, WeeklyPercentage = 10, TriggeringResetTime = now }
            ]
        };

        var sessionOnly = data.SessionSnapshots;

        Assert.Single(sessionOnly);
        Assert.Equal(42, sessionOnly[0].SessionPercentage);
    }

    [Fact]
    public void SessionSnapshots_SortsNewestFirst()
    {
        var older = DateTimeOffset.UtcNow.AddHours(-2);
        var newer = DateTimeOffset.UtcNow;
        var data = new UsageHistoryData
        {
            Snapshots =
            [
                new UsageSnapshot { Timestamp = older, ResetType = ResetType.SessionReset, SessionPercentage = 10, TriggeringResetTime = older },
                new UsageSnapshot { Timestamp = newer, ResetType = ResetType.SessionReset, SessionPercentage = 20, TriggeringResetTime = newer }
            ]
        };

        var sorted = data.SessionSnapshots;

        Assert.Equal(20, sorted[0].SessionPercentage);
        Assert.Equal(10, sorted[1].SessionPercentage);
    }

    [Fact]
    public void SessionSnapshots_ExcludesSnapshotsWhereTriggeringResetTimeIsMoreThanAMinuteAfterTimestamp()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var staleResetTime = timestamp.AddMinutes(5);
        var data = new UsageHistoryData
        {
            Snapshots = [new UsageSnapshot { Timestamp = timestamp, ResetType = ResetType.SessionReset, SessionPercentage = 10, TriggeringResetTime = staleResetTime }]
        };

        Assert.Empty(data.SessionSnapshots);
    }

    [Fact]
    public void AddSnapshot_AppendsToSnapshots()
    {
        var data = new UsageHistoryData();
        var snapshot = new UsageSnapshot { Timestamp = DateTimeOffset.UtcNow, ResetType = ResetType.SessionReset, SessionPercentage = 5, TriggeringResetTime = DateTimeOffset.UtcNow };

        data.AddSnapshot(snapshot);

        Assert.Single(data.Snapshots);
    }

    [Fact]
    public void ExportToCsv_IncludesHeaderAndOneRowPerSnapshot()
    {
        var timestamp = new DateTimeOffset(2026, 7, 18, 10, 30, 0, TimeSpan.Zero);
        var data = new UsageHistoryData
        {
            Snapshots = [new UsageSnapshot { Timestamp = timestamp, ResetType = ResetType.SessionReset, SessionPercentage = 42.5, TriggeringResetTime = timestamp }]
        };

        var csv = data.ExportToCsv();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.StartsWith("Timestamp,Reset Type,Session %,Weekly %,Opus %,Sonnet %", lines[0]);
        Assert.Contains("42.5", lines[1]);
    }

    [Fact]
    public void ExportToJson_ProducesParseableJsonContainingSnapshotData()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var data = new UsageHistoryData
        {
            Snapshots = [new UsageSnapshot { Timestamp = timestamp, ResetType = ResetType.WeeklyReset, WeeklyPercentage = 77, TriggeringResetTime = timestamp }]
        };

        var json = data.ExportToJson();

        Assert.Contains("77", json);
        var reparsed = System.Text.Json.JsonSerializer.Deserialize<UsageHistoryData>(json);
        Assert.Single(reparsed!.Snapshots);
    }
}
