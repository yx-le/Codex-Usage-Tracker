# Architecture

`TrackerController` owns the WPF windows, tray icon, one-second UI timer, 60-second read cadence, cancellation, and preference persistence. Quota reads run away from the UI thread and never overlap. Process discovery is paused during a quota read so its temporary `codex.exe` cannot create its own visibility trigger. Disposal cancels the active request and releases tray icon handles and system events.

`AppServerSource` is a short-lived stdio JSON-RPC client. It accepts only its matching response IDs, discards server diagnostics, limits request duration, and closes its own child process tree. It never drives a Codex conversation. `LocalCodexSource` is a bounded best-effort reader, and `QuotaParser` handles camel-case API fields and snake-case local quota fields. Unknown shapes degrade to N/A.

`UsageSnapshot` and `QuotaWindow` carry only observation time, source, percentages, durations, and reset times. The current reading lives in memory only. The desktop app has no history store, database dependency, or chart.

Theme brushes are dynamic WPF resources; custom rings repaint with current brushes. The application is single-instance per Windows session. It runs at ordinary user privilege and saves preferences in the Windows user profile.

Tests deliberately exercise failure semantics: wrong quota bucket, absent duration, expired reset, stale/future timestamps, duplicate notifications, partial JSONL records, missing windows, and corrupt preferences. The optional `--render-preview` mode renders real WPF windows using synthetic readings and an isolated data directory; it caught a WPF default two-way progress binding during initial validation.

Settings is a UserControl hosted in DetailsWindow; navigation switches content instead of opening an overlapping window. PanelPlacement chooses free space around the circle widget. For an edge bar, it opens toward the desktop: vertically centered beside a side bar, or horizontally centered below a top bar. It clamps the panel to the monitor work area and reduces its available size on compact displays without covering the bar. QuotaStatusPolicy supplies persistent warning colors, while ResetRefreshSchedule detects due reset timestamps and limits retries. The preview validates same-window navigation and renders warning, critical, exhausted, and tray states in both themes.

ResetRefreshSchedule retains each window's reset through missing data and ignores older fallback resets. WidgetPlacement clamps the native window rectangle to one actual monitor work area in physical pixels, excluding monitor gaps and taskbars.

The per-user startup entry launches a separate --watch-codex process with its own session mutex. It checks for a Codex process every two seconds and starts one tracker per observed Codex session. TaskbarStatusWindow is a minimized native taskbar button with TaskbarItemInfo progress; the notification-area icon remains circular. EdgeBarPlacement chooses the nearest supported edge after dragging. EdgeGlassBrush uses alpha 64/255, leaving approximately 75% background visibility. README desktop scenes are reproducible WPF compositions containing synthetic content.
