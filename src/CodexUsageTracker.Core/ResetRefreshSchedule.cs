namespace CodexUsageTracker.Core;

public sealed class ResetRefreshSchedule
{
    private readonly Dictionary<long, (DateTimeOffset LastAttempt, int Count)> attempts = [];

    // Called only when a read can start: once on crossing a reset, then 15s / 60s backoff.
    public bool ShouldRefresh(UsageSnapshot snapshot, DateTimeOffset now)
    {
        var due = new[] { snapshot.FiveHour?.ResetsAt, snapshot.Weekly?.ResetsAt }
            .Where(reset => reset.HasValue && reset.Value <= now).Select(reset => reset!.Value.ToUnixTimeSeconds()).Distinct().ToArray();
        var refresh = false;
        foreach (var reset in due)
        {
            if (!attempts.TryGetValue(reset, out var previous)
                || now - previous.LastAttempt >= TimeSpan.FromSeconds(previous.Count == 1 ? 15 : 60))
            { attempts[reset] = (now, previous.Count + 1); refresh = true; }
        }
        foreach (var old in attempts.Keys.Where(key => key < now.AddDays(-8).ToUnixTimeSeconds()).ToArray()) attempts.Remove(old);
        return refresh;
    }
}
