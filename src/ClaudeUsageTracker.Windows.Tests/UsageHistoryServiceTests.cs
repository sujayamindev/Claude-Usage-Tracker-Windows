using ClaudeUsageTracker.Windows.Models;
using ClaudeUsageTracker.Windows.Services;

namespace ClaudeUsageTracker.Windows.Tests;

public class UsageHistoryServiceTests
{
    private static string TempHistoryDir() =>
        Path.Combine(Path.GetTempPath(), $"history-test-{Guid.NewGuid():N}");

    private static ClaudeUsage MakeUsage(double sessionPct = 50, double weeklyPct = 30, double opusPct = 10, double sonnetPct = 20) => new()
    {
        SessionPercentage = sessionPct,
        SessionResetTime = DateTimeOffset.Now.AddHours(5),
        WeeklyPercentage = weeklyPct,
        WeeklyResetTime = DateTimeOffset.Now.AddDays(7),
        OpusWeeklyPercentage = opusPct,
        SonnetWeeklyPercentage = sonnetPct,
        LastUpdated = DateTimeOffset.Now
    };

    [Fact]
    public void LoadHistory_ReturnsEmptyWhenFileDoesNotExist()
    {
        var service = new UsageHistoryService(TempHistoryDir());

        var history = service.LoadHistory(Guid.NewGuid());

        Assert.Empty(history.Snapshots);
    }

    [Fact]
    public void RecordSessionReset_SkipsWhenPreviousUsageIsNullOrZero()
    {
        var dir = TempHistoryDir();
        try
        {
            var service = new UsageHistoryService(dir);
            var profileId = Guid.NewGuid();

            service.RecordSessionReset(profileId, null, DateTimeOffset.Now);
            service.RecordSessionReset(profileId, MakeUsage(sessionPct: 0), DateTimeOffset.Now);

            Assert.Empty(service.LoadHistory(profileId).Snapshots);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void RecordSessionReset_RecordsSnapshotOfPreviousUsage()
    {
        var dir = TempHistoryDir();
        try
        {
            var service = new UsageHistoryService(dir);
            var profileId = Guid.NewGuid();
            var resetTime = DateTimeOffset.Now;

            service.RecordSessionReset(profileId, MakeUsage(sessionPct: 87), resetTime);

            var snapshot = Assert.Single(service.LoadHistory(profileId).Snapshots);
            Assert.Equal(ResetType.SessionReset, snapshot.ResetType);
            Assert.Equal(87, snapshot.SessionPercentage);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void RecordWeeklyReset_RecordsSnapshotWithOpusAndSonnetBreakdown()
    {
        var dir = TempHistoryDir();
        try
        {
            var service = new UsageHistoryService(dir);
            var profileId = Guid.NewGuid();

            service.RecordWeeklyReset(profileId, MakeUsage(weeklyPct: 60, opusPct: 25, sonnetPct: 35), DateTimeOffset.Now);

            var snapshot = Assert.Single(service.LoadHistory(profileId).Snapshots);
            Assert.Equal(60, snapshot.WeeklyPercentage);
            Assert.Equal(25, snapshot.OpusWeeklyPercentage);
            Assert.Equal(35, snapshot.SonnetWeeklyPercentage);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void RecordSessionPeriodic_SkipsWhenRecordedTooRecently()
    {
        var dir = TempHistoryDir();
        try
        {
            var service = new UsageHistoryService(dir);
            var profileId = Guid.NewGuid();
            var t0 = DateTimeOffset.Now;

            service.RecordSessionPeriodic(profileId, MakeUsage(), t0);
            service.RecordSessionPeriodic(profileId, MakeUsage(), t0.AddMinutes(5));

            Assert.Single(service.LoadHistory(profileId).Snapshots);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void RecordSessionPeriodic_RecordsAgainAfterIntervalElapses()
    {
        var dir = TempHistoryDir();
        try
        {
            var service = new UsageHistoryService(dir);
            var profileId = Guid.NewGuid();
            var t0 = DateTimeOffset.Now;

            service.RecordSessionPeriodic(profileId, MakeUsage(), t0);
            service.RecordSessionPeriodic(profileId, MakeUsage(), t0.AddMinutes(11));

            Assert.Equal(2, service.LoadHistory(profileId).Snapshots.Count);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void RecordSessionPeriodic_PrunesOldestSnapshotsBeyondCap()
    {
        var dir = TempHistoryDir();
        try
        {
            var service = new UsageHistoryService(dir);
            var profileId = Guid.NewGuid();
            var t0 = DateTimeOffset.Now;

            for (var i = 0; i < 1002; i++)
                service.RecordSessionPeriodic(profileId, MakeUsage(sessionPct: i % 100 + 1), t0.AddMinutes(i * 10));

            Assert.Equal(1000, service.LoadHistory(profileId).Snapshots.Count);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void DeleteHistory_RemovesAllSnapshotsForProfile()
    {
        var dir = TempHistoryDir();
        try
        {
            var service = new UsageHistoryService(dir);
            var profileId = Guid.NewGuid();
            service.RecordSessionReset(profileId, MakeUsage(), DateTimeOffset.Now);

            service.DeleteHistory(profileId);

            Assert.Empty(service.LoadHistory(profileId).Snapshots);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Export_Json_ContainsRecordedData()
    {
        var dir = TempHistoryDir();
        try
        {
            var service = new UsageHistoryService(dir);
            var profileId = Guid.NewGuid();
            service.RecordSessionReset(profileId, MakeUsage(sessionPct: 42), DateTimeOffset.Now);

            var json = service.Export(profileId, HistoryExportFormat.Json);

            Assert.Contains("42", json);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Export_Csv_ContainsRecordedData()
    {
        var dir = TempHistoryDir();
        try
        {
            var service = new UsageHistoryService(dir);
            var profileId = Guid.NewGuid();
            service.RecordSessionReset(profileId, MakeUsage(sessionPct: 42), DateTimeOffset.Now);

            var csv = service.Export(profileId, HistoryExportFormat.Csv);

            Assert.Contains("42.0", csv);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
