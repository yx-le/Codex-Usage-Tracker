# Development and validation

[Back to the README](../README.md)

## Build and test

Install the **.NET 8 SDK** on Windows (8.0.400 or later in the .NET 8 family), then run:

```powershell
dotnet restore CodexUsageTracker.sln
dotnet build CodexUsageTracker.sln -c Release --no-restore
dotnet test CodexUsageTracker.sln -c Release --no-build
dotnet run --project src/CodexUsageTracker.App -c Release
```

Create a portable x64 build:

```powershell
dotnet publish src/CodexUsageTracker.App -c Release -r win-x64 --self-contained true -o artifacts/win-x64
```

The GitHub Actions workflow builds, tests, and uploads a portable Windows artifact. `.gitignore` excludes build outputs, local settings, databases, logs, and environment files.

Optional explicit live integration probe (prints quota fields only):

```powershell
dotnet run --project tools/CodexUsageTracker.Probe -- "C:\path\to\codex.exe"
```

Optional isolated UI render check (synthetic data; no quota request):

```powershell
dotnet run --project src/CodexUsageTracker.App -- --render-preview "C:\temp\tracker-preview" Dark
```

Use `Light` for the second theme. The preview creates isolated temporary settings/history beside its images. Close a running tracker first because the app is single-instance.

## Project structure

```text
src/CodexUsageTracker.Core/   Quota parsing, source adapters, pacing, alerts, SQLite, settings
src/CodexUsageTracker.App/    WPF windows, tray integration, theme, polling lifecycle
tests/CodexUsageTracker.Tests/  Parsing, fallback, retention, alerts, settings tests
tools/CodexUsageTracker.Probe/  Explicit live app-server integration check
.github/workflows/build.yml  Windows build, tests, portable artifact
docs/                       Architecture and synthetic UI previews
```

## Validation and limitations

The implementation was compiled and tested on Windows 11 x64 with .NET SDK 8.0.425. Seventy-nine automated tests cover bucket selection, unknown/malformed fields, stale readings, countdown boundaries, alert deduplication, local fallback, SQLite retention, settings recovery, click/drag handling at different display scales, quota status thresholds, non-overlapping panel placement, reset refresh retries through unavailable readings, monitor-gap recovery, and queued history operations under a SQLite write lock. Live app-server retrieval was verified with an existing Codex sign-in. Both themes and the settings panel are rendered for visual checks; widget pixels outside the circular edge are verified transparent. Windows 10 compatibility is targeted but has not been tested on a separate Windows 10 machine.

Codex process detection recognizes `codex.exe` (desktop app-server or CLI). The tracker's own temporary quota child is excluded from visibility decisions while it is reading. A background Codex process counts as running even if its main window is closed. There is no taskbar injection, browser scraping, automatic update service, or usage prediction based on token pricing. This is an independent utility, not an official OpenAI product.

Protocol reference: [Official Codex app-server documentation](https://developers.openai.com/codex/app-server).
