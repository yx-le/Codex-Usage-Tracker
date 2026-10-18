namespace CodexUsageTracker.Core;

public sealed record HistoryPoint(DateTimeOffset Time, double Remaining, DateTimeOffset? Reset);

public static class HistorySeries
{
    // Sort and deduplicate before drawing. A reset starts a new line, rather than
    // drawing an apparent consumption spike across unrelated quota windows.
    public static IReadOnlyList<IReadOnlyList<HistoryPoint>> Build(IEnumerable<UsageSnapshot> samples,
        Func<UsageSnapshot, QuotaWindow?> select, DateTimeOffset now, int days = 7)
    {
        var result = new List<IReadOnlyList<HistoryPoint>>();
        List<HistoryPoint>? segment = null;
        foreach (var sample in samples.Where(s => s.ObservedAt >= now.AddDays(-Math.Clamp(days, 1, 7)) && s.ObservedAt <= now)
            .GroupBy(s => s.ObservedAt.ToUnixTimeSeconds())
            .Select(g => g.OrderBy(s => select(s) is not null).ThenBy(s => s.Source == "Codex app-server").Last())
            .OrderBy(s => s.ObservedAt))
        {
            var window = select(sample);
            if (window is null || !double.IsFinite(window.UsedPercent) || window.UsedPercent is < 0 or > 100 || window.HasExpired(sample.ObservedAt))
            { segment = null; continue; }
            var point = new HistoryPoint(sample.ObservedAt, window.RemainingPercent, window.ResetsAt);
            var resetChanged = segment is not null && (segment[^1].Reset is { } before && point.Reset is { } after
                ? (after - before).Duration() > TimeSpan.FromMinutes(2) : segment[^1].Reset != point.Reset);
            if (segment is null || resetChanged || point.Time - segment[^1].Time > TimeSpan.FromMinutes(30))
            { segment = new(); result.Add(segment); }
            segment.Add(point);
        }
        return result;
    }
}
