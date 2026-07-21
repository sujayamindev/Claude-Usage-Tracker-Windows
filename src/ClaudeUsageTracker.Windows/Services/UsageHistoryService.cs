using System.IO;
using System.Text.Json;
using ClaudeUsageTracker.Windows.Models;

namespace ClaudeUsageTracker.Windows.Services;

public enum HistoryExportFormat { Json, Csv }

/// <summary>
/// Records and persists per-profile usage history as JSON files (not Credential Manager — history
/// is non-secret and can grow to non-trivial size). One file per profile under
/// %LOCALAPPDATA%\ClaudeUsageTracker\history\, mirroring the macOS app's file-based history
/// storage (UsageHistoryService.swift, moved off UserDefaults for the same size reason — #260).
/// Last-record-time gating for periodic snapshots is kept in-memory only (not persisted across
/// restarts): a restart may record one extra periodic snapshot inside the interval window, which
/// self-corrects on the next tick — not worth a second persisted-timestamp file for.
/// </summary>
public sealed class UsageHistoryService(string? historyDirectory = null)
{
    private const int MaxSessionSnapshots = 1000;
    private const int MaxWeeklySnapshots = 500;
    private static readonly TimeSpan SessionRecordingInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan WeeklyRecordingInterval = TimeSpan.FromHours(2);
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly string _historyDirectory = historyDirectory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClaudeUsageTracker", "history");

    private readonly Dictionary<Guid, DateTimeOffset> _lastSessionRecordTime = [];
    private readonly Dictionary<Guid, DateTimeOffset> _lastWeeklyRecordTime = [];

    private string HistoryFilePath(Guid profileId) => Path.Combine(_historyDirectory, $"usageHistory_{profileId:D}.json");

    public UsageHistoryData LoadHistory(Guid profileId)
    {
        var path = HistoryFilePath(profileId);
        if (!File.Exists(path))
            return new UsageHistoryData();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<UsageHistoryData>(json) ?? new UsageHistoryData();
        }
        catch (JsonException)
        {
            return new UsageHistoryData();
        }
    }

    public void SaveHistory(UsageHistoryData history, Guid profileId)
    {
        Directory.CreateDirectory(_historyDirectory);
        File.WriteAllText(HistoryFilePath(profileId), JsonSerializer.Serialize(history, WriteOptions));
    }

    public void RecordSessionReset(Guid profileId, ClaudeUsage? previousUsage, DateTimeOffset resetTime)
    {
        if (previousUsage is null || previousUsage.SessionPercentage <= 0)
            return;

        var history = LoadHistory(profileId);
        history.AddSnapshot(UsageSnapshot.FromSessionReset(previousUsage, resetTime));
        PruneSession(history);
        SaveHistory(history, profileId);
    }

    public void RecordWeeklyReset(Guid profileId, ClaudeUsage? previousUsage, DateTimeOffset resetTime)
    {
        if (previousUsage is null || previousUsage.WeeklyPercentage <= 0)
            return;

        var history = LoadHistory(profileId);
        history.AddSnapshot(UsageSnapshot.FromWeeklyReset(previousUsage, resetTime));
        PruneWeekly(history);
        SaveHistory(history, profileId);
    }

    public void RecordSessionPeriodic(Guid profileId, ClaudeUsage usage, DateTimeOffset? now = null)
    {
        var currentTime = now ?? DateTimeOffset.Now;
        if (_lastSessionRecordTime.TryGetValue(profileId, out var last) && currentTime - last < SessionRecordingInterval)
            return;

        var history = LoadHistory(profileId);
        history.AddSnapshot(new UsageSnapshot
        {
            Timestamp = currentTime,
            ResetType = ResetType.SessionReset,
            SessionPercentage = usage.SessionPercentage,
            TriggeringResetTime = currentTime
        });
        PruneSession(history);
        SaveHistory(history, profileId);
        _lastSessionRecordTime[profileId] = currentTime;
    }

    public void RecordWeeklyPeriodic(Guid profileId, ClaudeUsage usage, DateTimeOffset? now = null)
    {
        var currentTime = now ?? DateTimeOffset.Now;
        if (_lastWeeklyRecordTime.TryGetValue(profileId, out var last) && currentTime - last < WeeklyRecordingInterval)
            return;

        var history = LoadHistory(profileId);
        history.AddSnapshot(new UsageSnapshot
        {
            Timestamp = currentTime,
            ResetType = ResetType.WeeklyReset,
            WeeklyPercentage = usage.WeeklyPercentage,
            OpusWeeklyPercentage = usage.OpusWeeklyPercentage,
            SonnetWeeklyPercentage = usage.SonnetWeeklyPercentage,
            TriggeringResetTime = currentTime
        });
        PruneWeekly(history);
        SaveHistory(history, profileId);
        _lastWeeklyRecordTime[profileId] = currentTime;
    }

    private static void PruneSession(UsageHistoryData history)
    {
        var sessionSnapshots = history.SessionSnapshots;
        if (sessionSnapshots.Count <= MaxSessionSnapshots)
            return;

        var idsToRemove = sessionSnapshots.Skip(MaxSessionSnapshots).Select(s => s.Id).ToHashSet();
        history.Snapshots.RemoveAll(s => idsToRemove.Contains(s.Id));
    }

    private static void PruneWeekly(UsageHistoryData history)
    {
        var weeklySnapshots = history.WeeklySnapshots;
        if (weeklySnapshots.Count <= MaxWeeklySnapshots)
            return;

        var idsToRemove = weeklySnapshots.Skip(MaxWeeklySnapshots).Select(s => s.Id).ToHashSet();
        history.Snapshots.RemoveAll(s => idsToRemove.Contains(s.Id));
    }

    public void DeleteHistory(Guid profileId)
    {
        var path = HistoryFilePath(profileId);
        if (File.Exists(path))
            File.Delete(path);
        _lastSessionRecordTime.Remove(profileId);
        _lastWeeklyRecordTime.Remove(profileId);
    }

    public string Export(Guid profileId, HistoryExportFormat format)
    {
        var history = LoadHistory(profileId);
        return format switch
        {
            HistoryExportFormat.Json => history.ExportToJson(),
            HistoryExportFormat.Csv => history.ExportToCsv(),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }
}
