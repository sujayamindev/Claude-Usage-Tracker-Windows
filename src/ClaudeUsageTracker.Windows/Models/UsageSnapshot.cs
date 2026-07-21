using System.Text;
using System.Text.Json;

namespace ClaudeUsageTracker.Windows.Models;

public enum ResetType
{
    SessionReset,
    WeeklyReset
}

/// <summary>
/// A recorded usage percentage at a point in time. Ported from the macOS app's UsageSnapshot
/// (Session/Weekly/Opus/Sonnet percentages only — no token counts, no API billing, no
/// Design/Fable fields, since Windows' ClaudeUsage never carried those).
/// </summary>
public sealed record UsageSnapshot
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required DateTimeOffset Timestamp { get; init; }
    public required ResetType ResetType { get; init; }
    public double? SessionPercentage { get; init; }
    public double? WeeklyPercentage { get; init; }
    public double? OpusWeeklyPercentage { get; init; }
    public double? SonnetWeeklyPercentage { get; init; }
    public required DateTimeOffset TriggeringResetTime { get; init; }

    public static UsageSnapshot FromSessionReset(ClaudeUsage usage, DateTimeOffset resetTime) => new()
    {
        Timestamp = DateTimeOffset.Now,
        ResetType = ResetType.SessionReset,
        SessionPercentage = usage.SessionPercentage,
        TriggeringResetTime = resetTime
    };

    public static UsageSnapshot FromWeeklyReset(ClaudeUsage usage, DateTimeOffset resetTime) => new()
    {
        Timestamp = DateTimeOffset.Now,
        ResetType = ResetType.WeeklyReset,
        WeeklyPercentage = usage.WeeklyPercentage,
        OpusWeeklyPercentage = usage.OpusWeeklyPercentage,
        SonnetWeeklyPercentage = usage.SonnetWeeklyPercentage,
        TriggeringResetTime = resetTime
    };
}

/// <summary>Container for a profile's usage history, plus filtering/sorting/export helpers.</summary>
public sealed class UsageHistoryData
{
    public List<UsageSnapshot> Snapshots { get; set; } = [];

    public IReadOnlyList<UsageSnapshot> SessionSnapshots => FilteredAndSorted(ResetType.SessionReset);

    public IReadOnlyList<UsageSnapshot> WeeklySnapshots => FilteredAndSorted(ResetType.WeeklyReset);

    private IReadOnlyList<UsageSnapshot> FilteredAndSorted(ResetType type) =>
        Snapshots
            .Where(s => s.ResetType == type)
            .Where(s => s.TriggeringResetTime <= s.Timestamp.AddSeconds(60))
            .OrderByDescending(s => s.Timestamp)
            .ToList();

    public void AddSnapshot(UsageSnapshot snapshot) => Snapshots.Add(snapshot);

    private static readonly JsonSerializerOptions ExportJsonOptions = new() { WriteIndented = true };

    public string ExportToJson() => JsonSerializer.Serialize(this, ExportJsonOptions);

    public string ExportToCsv()
    {
        var csv = new StringBuilder();
        csv.AppendLine("Timestamp,Reset Type,Session %,Weekly %,Opus %,Sonnet %");

        foreach (var snapshot in Snapshots.OrderByDescending(s => s.Timestamp))
        {
            var timestamp = snapshot.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            var sessionPct = snapshot.SessionPercentage?.ToString("F1") ?? "";
            var weeklyPct = snapshot.WeeklyPercentage?.ToString("F1") ?? "";
            var opusPct = snapshot.OpusWeeklyPercentage?.ToString("F1") ?? "";
            var sonnetPct = snapshot.SonnetWeeklyPercentage?.ToString("F1") ?? "";
            csv.AppendLine($"{timestamp},{snapshot.ResetType},{sessionPct},{weeklyPct},{opusPct},{sonnetPct}");
        }

        return csv.ToString();
    }
}
