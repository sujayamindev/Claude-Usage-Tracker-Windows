# Claude Usage Tracker (Windows)

A native Windows tray application for real-time monitoring of Claude AI usage limits — session (5-hour window), weekly, and per-model (Opus/Sonnet) consumption.

This is a **Windows port** of [Claude Usage Tracker](https://github.com/hamed-elfayome/Claude-Usage-Tracker), a macOS menu bar app built with Swift/SwiftUI/AppKit. AppKit, SwiftUI, and Keychain Services have no Windows equivalent, so this isn't a shared-codebase build — it's a separate WPF/.NET application that ports the underlying logic (usage parsing, status thresholds, session-key validation) onto a Windows-native UI stack. All credit for the original design, feature set, and API integration approach belongs to the [macOS project](https://github.com/hamed-elfayome/Claude-Usage-Tracker) and its contributors.

## Status

Early-stage MVP. Currently supports:

- Multi-profile support — add unlimited Claude profiles, each with isolated credentials and its own auth mode; switch, rename, or delete them from "Manage Profiles…" in the tray menu
- Three ways to sign in: browser-based sign-in (embedded, in-app login flow), a manual session key, or automatically if you're already logged into Claude Code CLI (no setup needed in that case)
- Live tray icon showing session usage — three styles available: circular progress ring (default), horizontal progress bar, and compact status dot; plus color modes (multi-color/monochrome/custom), a pace marker, and a remaining-vs-used display toggle, all from "Icon Style…" in the tray menu
- A popover with session/weekly/Opus/Sonnet usage bars, reset countdowns with actual clock times (e.g. "Resets in 1h 42m · 3:45 PM"), a live Claude system status indicator, and organization info — pin it open or detach it into its own resizable window
- History window ("History…" in the tray menu), with two tabs: usage history (session and weekly usage tracked over time as pan-navigable charts, with JSON/CSV export) and Claude Code cost stats (all-time and per-day cost totals sourced from Claude Code CLI's own reported cost; requires a small addition to your own statusline script to record cost per session)
- Threshold notifications at 75/90/95% (and a custom threshold), with sound on/off toggle
- Claude Code CLI terminal statusline integration (toggle from the tray menu) showing live session/weekly usage alongside your current directory/model in the terminal prompt
- Launch-at-Windows-startup toggle

**Not yet implemented** (present in the macOS app, planned or deferred here): multi-profile tray display, auto-switch on session limit, global keyboard shortcuts, and API console cost/overage tracking.

## Requirements

- Windows 10/11
- .NET 10 runtime (or build self-contained — see below)
- A Claude AI account with a valid `sessionKey` cookie value from claude.ai

## Signing in

If you're already logged into [Claude Code CLI](https://docs.claude.com/en/docs/claude-code) on this machine, the app detects that automatically on first launch and skips setup entirely.

Otherwise, use "Sign in with browser" in the setup window — it opens an embedded login page and picks up your session automatically once you log in, no manual copying needed.

You can also provide a `sessionKey` manually:

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

## Building the installer

An Inno Setup script (`installer/setup.iss`) packages a self-contained build into a Windows installer with Start Menu/desktop shortcuts and an uninstaller. Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`ISCC.exe` on your `PATH`).

```bash
# 1. Publish a self-contained build
dotnet publish src/ClaudeUsageTracker.Windows/ClaudeUsageTracker.Windows.csproj \
  -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false \
  -o installer/publish

# 2. Compile the installer
ISCC installer/setup.iss
```

The installer is written to `installer/output/ClaudeUsageTracker-Setup-<version>.exe`. Note: the app requires the Microsoft Edge WebView2 Runtime (bundled with Windows 11; the installer warns if it's missing on Windows 10 and links to the download).

## Architecture notes

`claude.ai/api` sits behind Cloudflare bot-management that fingerprints the TLS/HTTP client — a plain `HttpClient` gets served a JS challenge page instead of real API responses. This app works around that by routing API calls through a hidden WebView2 control (a real Chromium engine), so requests genuinely originate from a browser-grade network stack. See `docs/superpowers/specs/` for the full design history and rationale behind this and other decisions (UI stack choice, styling approach, feature scope).

## License

MIT — see [LICENSE](LICENSE). This project is a derivative port of [Claude Usage Tracker](https://github.com/hamed-elfayome/Claude-Usage-Tracker), also MIT-licensed.

## Disclaimer

This application is not affiliated with, endorsed by, or sponsored by Anthropic PBC. Claude is a trademark of Anthropic PBC. This is an independent third-party tool created for personal usage monitoring.
