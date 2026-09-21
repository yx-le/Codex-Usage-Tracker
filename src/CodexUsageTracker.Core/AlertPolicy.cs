namespace CodexUsageTracker.Core;

public sealed class AlertPolicy
{
    private readonly HashSet<string> delivered = [];
    public IReadOnlyList<string> Evaluate(UsageSnapshot snapshot, TrackerSettings settings, DateTimeOffset now)
    {
        var messages = new List<string>();
        if (!settings.AlertsEnabled || snapshot.Source != "Codex app-server" || snapshot.IsStale(now)) return messages;
        foreach (var (label, window) in new[] { ("5-hour", snapshot.FiveHour), ("Weekly", snapshot.Weekly) })
        {
            if (window is null || window.HasExpired(now) || window.ResetsAt is null) continue;
            foreach (var threshold in new[] { settings.CriticalPercent, settings.WarningPercent }.Distinct())
            {
                var key = $"{label}:{window.ResetsAt}:{threshold}";
                if (window.RemainingPercent > threshold || !delivered.Add(key)) continue;
                messages.Add($"{label} quota: {window.RemainingPercent:0}% remaining. {window.Countdown(now)}.");
                // A critical notification also covers the warning level for this window.
                if (threshold == settings.CriticalPercent) delivered.Add($"{label}:{window.ResetsAt}:{settings.WarningPercent}");
                break;
            }
        }
        return messages;
    }
}
