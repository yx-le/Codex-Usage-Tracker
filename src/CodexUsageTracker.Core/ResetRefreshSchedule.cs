namespace CodexUsageTracker.Core;

public sealed class ResetRefreshSchedule
{
    private readonly Dictionary<long, (DateTimeOffset LastAttempt, int Count)> attempts = [];
    private readonly Dictionary<int, DateTimeOffset> knownResets = [];

    // Called only when a read can start: once on crossing a reset, then 15s / 60s backoff.
    public bool ShouldRefresh(UsageSnapshot snapshot, DateTimeOffset now)
    {
        // Missing data must not cancel a pending reset. Only a later reset for the
        // same quota window confirms that the source has moved to a new window.
        foreach (var window in new[] { snapshot.FiveHour, snapshot.Weekly })
            if (window?.ResetsAt is { } reset && (!knownResets.TryGetValue(window.DurationMinutes, out var known) || reset > known))
                knownResets[window.DurationMinutes] = reset;
        var due = knownResets.Values.Where(reset => reset <= now && reset >= now.AddDays(-8))
            .Select(reset => reset.ToUnixTimeSeconds()).Distinct().ToArray();
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
