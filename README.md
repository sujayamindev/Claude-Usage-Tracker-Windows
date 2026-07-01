# Claude Usage Tracker (Windows)

A native Windows tray application for real-time monitoring of Claude AI usage limits — session (5-hour window), weekly, and per-model (Opus/Sonnet) consumption.

This is a **Windows port** of [Claude Usage Tracker](https://github.com/hamed-elfayome/Claude-Usage-Tracker), a macOS menu bar app built with Swift/SwiftUI/AppKit. AppKit, SwiftUI, and Keychain Services have no Windows equivalent, so this isn't a shared-codebase build — it's a separate WPF/.NET application that ports the underlying logic (usage parsing, status thresholds, session-key validation) onto a Windows-native UI stack. All credit for the original design, feature set, and API integration approach belongs to the [macOS project](https://github.com/hamed-elfayome/Claude-Usage-Tracker) and its contributors.

## Status

Early-stage MVP. Currently supports:

- A single Claude profile (session-key authentication)
- Live tray icon showing session usage as a circular progress ring
- A popover with session/weekly/Opus/Sonnet usage bars, reset-time countdowns, a live Claude system status indicator, and organization info
- Launch-at-Windows-startup toggle

**Not yet implemented** (present in the macOS app, planned or deferred here): multi-profile support, usage history/charts, Claude Code CLI credential sync, global keyboard shortcuts, threshold notifications, CLI OAuth login, API console cost/overage tracking, and installer packaging.

## Requirements

- Windows 10/11
- .NET 10 runtime (or build self-contained — see below)
- A Claude AI account with a valid `sessionKey` cookie value from claude.ai

## Getting your session key

1. Open [claude.ai](https://claude.ai) in your browser and make sure you're logged in.
2. Open Developer Tools (`F12`).
3. Go to **Application** (Chrome/Edge) or **Storage** (Firefox) → **Cookies** → `https://claude.ai`.
4. Copy the value of the `sessionKey` cookie (starts with `sk-ant-sid01-...`).
5. Paste it into the app's setup window on first launch.

## Building from source

```bash
git clone <this-repo-url>
cd Claude-Usage-Tracker-Windows

dotnet build src/ClaudeUsageTracker.Windows/ClaudeUsageTracker.Windows.csproj -c Debug
```

Run the built executable at `src/ClaudeUsageTracker.Windows/bin/Debug/net10.0-windows/ClaudeUsageTracker.Windows.exe`.

### Running tests

```bash
dotnet test src/ClaudeUsageTracker.Windows.Tests/ClaudeUsageTracker.Windows.Tests.csproj
```

## Architecture notes

`claude.ai/api` sits behind Cloudflare bot-management that fingerprints the TLS/HTTP client — a plain `HttpClient` gets served a JS challenge page instead of real API responses. This app works around that by routing API calls through a hidden WebView2 control (a real Chromium engine), so requests genuinely originate from a browser-grade network stack. See `docs/superpowers/specs/` for the full design history and rationale behind this and other decisions (UI stack choice, styling approach, feature scope).

## License

MIT — see [LICENSE](LICENSE). This project is a derivative port of [Claude Usage Tracker](https://github.com/hamed-elfayome/Claude-Usage-Tracker), also MIT-licensed.

## Disclaimer

This application is not affiliated with, endorsed by, or sponsored by Anthropic PBC. Claude is a trademark of Anthropic PBC. This is an independent third-party tool created for personal usage monitoring.
