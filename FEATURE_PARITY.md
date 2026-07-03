# Feature Parity Tracker (macOS vs Windows)

Tracks feature gaps between the mature macOS app ([Claude-Usage-Tracker](https://github.com/hamed-elfayome/Claude-Usage-Tracker), v3.1.1) and this Windows port. Update checkboxes as items get implemented. See `docs/superpowers/specs/2026-07-01-windows-port-design.md` for the original MVP scope decision — items here may still be intentionally deferred, not just "not done yet."

Last compared: 2026-07-02.

## Comparison table

| Area | macOS app | Windows app |
|---|---|---|
| Core usage tracking | Session (5h), weekly, Opus, API console cost, extra usage | Session (5h), weekly, Opus, Sonnet |
| Profiles | Unlimited profiles, multi-profile menu bar display, auto-switch on limit | Single profile only |
| Auth methods | Claude Code CLI auto-detect, browser sign-in (embedded WebView), manual session key, separate API console key | Manual session key, Claude Code CLI auto-detect (fallback) |
| Claude Code CLI integration | Full sync (credentials, auto-switch, statusline installer) | None |
| Usage history/charts | Interactive charts (session/weekly/billing over time) | None |
| Menu bar/tray icon | 5 icon styles, 3 color modes, 6-tier pace markers, multi-metric icons | Single circular progress ring style, ring color reflects status |
| Popover | Full popover with profile switcher, quick actions, detachable window | Session/weekly/Opus/Sonnet bars, reset countdowns, org info, live status indicator |
| System status | Live status from status.claude.com | Implemented |
| Notifications | Threshold alerts (75/90/95% + custom) with sound picker | None |
| Auto-start sessions | Yes, per-profile | Not implemented |
| Global keyboard shortcuts | System-wide, no Accessibility permission needed | Not implemented |
| Localization | 13 languages | English only |
| Auto-update | Sparkle-based | GitHub Releases-based, implemented (startup check + tray menu check + silent install) |
| Launch at login/startup | Yes | Yes (registry `Run` key) |
| Credential storage | macOS Keychain | Windows Credential Manager |
| Headless mode | Supported (Remote Desktop) | N/A — concept doesn't map the same way on Windows |
| API console cost tracking | Full (billing, per-key breakdown, daily chart) | Not implemented |

## Checklist

### Auth & profiles
- [ ] Multi-profile support (unlimited profiles, isolated credentials/settings)
- [ ] Multi-profile tray display (all profiles shown at once)
- [ ] Auto-switch profile on session limit reached
- [x] Claude Code CLI credential auto-detect / OAuth sync
- [ ] Browser-based sign-in (embedded WebView extracts session key automatically)
- [ ] Separate API console key + org ID (dual tracking: web + API)

### Usage tracking
- [ ] API console usage/cost tracking (monthly cost, per-key breakdown, daily chart)
- [ ] Extra usage / overage cost tracking
- [ ] Usage history persistence + charts (session/weekly/billing over time)

### Tray icon & UI
- [ ] Multiple icon styles (battery, progress bar, percentage-only, icon+bar, compact)
- [ ] Color mode options (multi-color / greyscale / single custom color)
- [ ] 6-tier pace markers (projected usage pace indicator)
- [ ] Remaining vs. used percentage display toggle
- [ ] Detachable/floating popover window

### Automation
- [ ] Auto-start new session when usage resets to 0%
- [ ] Threshold notifications (75/90/95% + custom, with sound picker)
- [ ] Global keyboard shortcuts (no Accessibility-equivalent permission needed)

### Developer integration
- [ ] Claude Code terminal statusline integration (installer + live components)

### Platform/UX polish
- [ ] Localization (currently English only vs. 13 languages on macOS)

### Already at parity
- [x] Live tray status ring reflecting session usage
- [x] Popover with session/weekly/Opus/Sonnet bars, reset countdowns, org info
- [x] Live Claude system status indicator (status.claude.com)
- [x] Launch-at-startup toggle
- [x] Secure credential storage (Windows Credential Manager, equivalent to Keychain)
- [x] Auto-update (GitHub Releases-based silent install)
