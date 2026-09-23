# Data sources and privacy

[Back to the README](../README.md)

## How readings are obtained

1. **Codex app-server.** A short-lived `codex app-server --listen stdio://` child performs the `initialize` / `initialized` handshake and calls only `account/rateLimits/read`. The tracker explicitly disables analytics and OpenTelemetry exporters on this child. It does not create threads, run prompts, reset credits, or modify account settings. The child is terminated after the read or a 20-second timeout.
2. **Local Codex fallback.** If app-server data is unavailable, inspect the final 1 MiB of up to 24 recent session JSONL files under `$CODEX_HOME/sessions` or `~/.codex/sessions`. Only `event_msg` / `token_count` records with `rate_limits` are converted into quota measurements. Existing session files contain other content; the tracker transiently scans lines but does not retain, log, display, or copy prompt/response content. Their format is not a stable public API.
3. **N/A.** If neither source yields a supported quota window, display N/A. Never infer a quota percentage from tokens or assume that missing data means zero usage.

The multi-bucket response's `codex` entry takes precedence. Other model-specific buckets are not substituted. Durations must explicitly match 300 or 10,080 minutes; unsupported or missing durations remain N/A. Missing reset times remain unknown. A past reset becomes “Reset due · awaiting update,” not an assumed quota refill.

Quota refresh runs every 60 seconds while Codex runs; the countdown updates every second. A known reset triggers an immediate refresh, with a 15-second retry followed by 60-second retries if the source still reports the expired window. Pending resets survive unavailable or partial readings until a later reset is reported for that quota window; abandoned retries expire after eight days. Reads never overlap and an expired reading never implies a refill. Manual refresh works from the tray or panel. Data older than three minutes is STALE. Local fallback is always labelled cached/local. Low-quota alerts only use fresh app-server readings and are deduplicated per threshold and reset window during the running session. Restarting the tracker can repeat a still-applicable warning. Windows notification settings may suppress tray balloons.

The pace estimate extrapolates the window-average usage rate. “Above sustainable pace” means projected consumption exceeds 105% of the quota by reset, allowing a small tolerance. It is unavailable for stale, expired, or unknown-reset data and says “Learning pace” during the first five minutes. It is advisory; it cannot predict future work.

Local files live in `%LOCALAPPDATA%\CodexUsageTracker`:

| File | Contents |
| --- | --- |
| `settings.json` | UI preferences, thresholds, widget location, optional Codex executable path |
| `usage.db` (+ SQLite WAL files) | Observation time, source label, used percentages, reset timestamps |

No identity, prompt text, response text, tokens, cookies, or keys are stored by the tracker. Seven-day retention is applied at startup and refresh; if the app is closed, expired records are removed the next time it runs. **Clear local history** removes all readings. The database uses the normal Windows user profile permissions; it is not separately encrypted. History follows this local profile, so clear it when switching Codex accounts if you want separate charts.

The tracker has no direct HTTP client or telemetry endpoint. Codex itself contacts its services to retrieve quota; that connection is necessary for live readings.

