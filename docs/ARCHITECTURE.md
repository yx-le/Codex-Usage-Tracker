# Architecture

`TrackerController` owns the WPF windows, tray icon, one-second UI timer, 60-second read cadence, cancellation, and persistence. Quota reads run away from the UI thread and never overlap. Process discovery is paused during a quota read so its temporary `codex.exe` cannot create its own visibility trigger. Disposal cancels the active request and releases tray icon handles and system events.

`AppServerSource` is a short-lived stdio JSON-RPC client. It accepts only its matching response IDs, discards server diagnostics, limits request duration, and closes its own child process tree. It never drives a Codex conversation. `LocalCodexSource` is a bounded best-effort reader, and `QuotaParser` handles camel-case API fields and snake-case local quota fields. Unknown shapes degrade to N/A.

`UsageSnapshot` and `QuotaWindow` carry only observation time, source, percentages, durations, and reset times. The fixed SQLite schema cannot accept arbitrary session content. Inserts use parameters and a timestamp/source key to avoid duplicating a cached fallback reading. History is not treated as a live source. The chart uses actual observation times and skips lines across gaps longer than ten minutes or changed resets.

Theme brushes are dynamic WPF resources; custom rings and chart repaint with current brushes. The application is single-instance per Windows session. It runs at ordinary user privilege and keeps its history outside the repository.

Tests deliberately exercise failure semantics: wrong quota bucket, absent duration, expired reset, stale/future timestamps, duplicate notifications, partial JSONL records, seven-day pruning, missing windows, and corrupt preferences. The optional `--render-preview` mode renders real WPF windows using synthetic readings and an isolated data directory; it caught a WPF default two-way progress binding during initial validation.

Settings is a UserControl hosted in DetailsWindow; navigation switches content instead of opening an overlapping window. PanelPlacement chooses free space around the widget. QuotaStatusPolicy supplies persistent warning colors, while ResetRefreshSchedule detects due reset timestamps and limits retries. The preview validates same-window navigation and renders warning, critical, exhausted, and tray states in both themes.

AsyncUsageRepository initializes and accesses SQLite on background workers through a shared semaphore. Saves, reads and clears are serialized, and the controller ignores superseded history results. SQLite lock waits are limited to two seconds. ResetRefreshSchedule retains each window's reset through missing data and ignores older fallback resets. WidgetPlacement clamps the native window rectangle to one actual monitor work area in physical pixels, excluding monitor gaps and taskbars.
