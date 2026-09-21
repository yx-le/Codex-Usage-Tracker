using System.Text.Json;

namespace CodexUsageTracker.Core;

public static class QuotaParser
{
    public static UsageSnapshot Parse(JsonElement root, DateTimeOffset observedAt, string source)
    {
        if (root.ValueKind != JsonValueKind.Object) return UsageSnapshot.Unavailable(observedAt);
        if (root.TryGetProperty("result", out var result)) root = result;
        if (root.ValueKind != JsonValueKind.Object) return UsageSnapshot.Unavailable(observedAt);
        JsonElement bucket;
        // A model-specific bucket must never silently become the account's Codex quota.
        if (root.TryGetProperty("rateLimitsByLimitId", out var map) && map.ValueKind == JsonValueKind.Object)
        {
            if (!map.TryGetProperty("codex", out bucket)) return UsageSnapshot.Unavailable(observedAt);
        }
        else if (root.TryGetProperty("rateLimits", out var legacy)) bucket = legacy;
        else if (root.TryGetProperty("rate_limits", out var local)) bucket = local;
        else bucket = root;
        if (bucket.ValueKind != JsonValueKind.Object) return UsageSnapshot.Unavailable(observedAt);
        var id = Get(bucket, "limitId", "limit_id");
        if (id.ValueKind == JsonValueKind.String && id.GetString() is { } value && value != "codex")
            return UsageSnapshot.Unavailable(observedAt);
        QuotaWindow? five = null, week = null;
        foreach (var name in new[] { "primary", "secondary" })
        {
            if (!bucket.TryGetProperty(name, out var window) || window.ValueKind != JsonValueKind.Object) continue;
            var used = Get(window, "usedPercent", "used_percent");
            var duration = Get(window, "windowDurationMins", "window_minutes");
            var reset = Get(window, "resetsAt", "resets_at");
            if (used.ValueKind != JsonValueKind.Number || !used.TryGetDouble(out var percent) || !double.IsFinite(percent)
                || duration.ValueKind != JsonValueKind.Number || !duration.TryGetInt32(out var minutes)) continue;
            DateTimeOffset? resetTime = null;
            if (reset.ValueKind == JsonValueKind.Number && reset.TryGetInt64(out var seconds))
            {
                try { resetTime = DateTimeOffset.FromUnixTimeSeconds(seconds); }
                catch (ArgumentOutOfRangeException) { /* Unknown reset stays unknown. */ }
            }
            var parsed = new QuotaWindow(Math.Clamp(percent, 0, 100), minutes, resetTime);
            if (minutes == 300) five = parsed;
            if (minutes == 10080) week = parsed;
        }
        return new(observedAt, source, five, week);
    }

    private static JsonElement Get(JsonElement element, string camel, string snake) =>
        element.TryGetProperty(camel, out var value) || element.TryGetProperty(snake, out value) ? value : default;
}
