namespace ClaudeUsageTracker.Windows.Models;

/// <summary>
/// One deduped ledger row: a Claude Code session's final cost, as computed by Claude Code CLI
/// itself (cost.total_cost_usd from the statusline stdin JSON) — never recomputed from token
/// counts or a pricing table by this app. See
/// docs/superpowers/specs/2026-07-24-claude-code-cost-stats-design.md.
/// </summary>
public sealed record CostLedgerEntry(string SessionId, decimal CostUsd, DateTimeOffset Timestamp);
