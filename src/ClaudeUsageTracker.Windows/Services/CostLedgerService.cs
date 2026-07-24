using System.IO;
using System.Linq;
using System.Text.Json;
using ClaudeUsageTracker.Windows.Models;

namespace ClaudeUsageTracker.Windows.Services;

/// <summary>
/// Reads the NDJSON cost ledger appended to by the user's statusline.ps1 (outside this repo —
/// see docs/superpowers/specs/2026-07-24-claude-code-cost-stats-design.md), dedupes it to one
/// row per session (keeping the max cost seen, since a session's cost only grows), and compacts
/// the file back to disk so it tracks session count rather than turn count.
/// </summary>
public sealed class CostLedgerService(string? ledgerFilePath = null)
{
    private readonly string _ledgerFilePath = ledgerFilePath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClaudeUsageTracker", "costLedger.ndjson");

    public IReadOnlyList<CostLedgerEntry> LoadAndCompact()
    {
        if (!File.Exists(_ledgerFilePath))
            return [];

        var deduped = new Dictionary<string, CostLedgerEntry>();
        foreach (var line in File.ReadAllLines(_ledgerFilePath))
        {
            var entry = TryParseLine(line);
            if (entry is null)
                continue;

            if (!deduped.TryGetValue(entry.SessionId, out var existing) || entry.CostUsd > existing.CostUsd)
                deduped[entry.SessionId] = entry;
        }

        var result = deduped.Values.ToList();
        WriteCompacted(result);
        return result;
    }

    public decimal GetAllTimeTotal(IReadOnlyList<CostLedgerEntry> entries) =>
        entries.Sum(e => e.CostUsd);

    public IReadOnlyList<(DateOnly Date, decimal Total)> GetDailyTotals(IReadOnlyList<CostLedgerEntry> entries) =>
        entries
            .GroupBy(e => DateOnly.FromDateTime(e.Timestamp.LocalDateTime))
            .Select(g => (Date: g.Key, Total: g.Sum(e => e.CostUsd)))
            .OrderByDescending(x => x.Date)
            .ToList();

    private static CostLedgerEntry? TryParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return null;
        }

        using (doc)
        {
            var root = doc.RootElement;

            if (!root.TryGetProperty("sessionId", out var sessionIdProp) ||
                sessionIdProp.ValueKind != JsonValueKind.String ||
                sessionIdProp.GetString() is not { Length: > 0 } sessionId)
                return null;

            if (!root.TryGetProperty("costUsd", out var costProp) || !costProp.TryGetDecimal(out var cost))
                return null;

            if (!root.TryGetProperty("timestamp", out var timestampProp) ||
                timestampProp.ValueKind != JsonValueKind.String ||
                !DateTimeOffset.TryParse(timestampProp.GetString(), out var timestamp))
                return null;

            return new CostLedgerEntry(sessionId, cost, timestamp);
        }
    }

    private void WriteCompacted(IReadOnlyList<CostLedgerEntry> entries)
    {
        var directory = Path.GetDirectoryName(_ledgerFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var lines = entries.Select(e => JsonSerializer.Serialize(new
        {
            sessionId = e.SessionId,
            costUsd = e.CostUsd,
            timestamp = e.Timestamp.ToString("o")
        }));
        File.WriteAllLines(_ledgerFilePath, lines);
    }
}
