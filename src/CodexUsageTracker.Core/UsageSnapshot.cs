namespace CodexUsageTracker.Core;

public sealed record QuotaWindow(double UsedPercent, int DurationMinutes, DateTimeOffset? ResetsAt)
{
    public double RemainingPercent => Math.Clamp(100 - UsedPercent, 0, 100);
    public bool HasExpired(DateTimeOffset now) => ResetsAt is { } reset && reset <= now;
    public string Countdown(DateTimeOffset now)
    {
        if (ResetsAt is not { } reset) return "Reset time unavailable";
        var delta = reset - now;
        if (delta <= TimeSpan.Zero) return "Reset due · awaiting update";
        if (delta.TotalMinutes < 1) return "Resets in less than 1 minute";
        return delta.TotalDays >= 1 ? $"Resets in {(int)delta.TotalDays}d {delta.Hours}h"
            : $"Resets in {(int)delta.TotalHours}h {delta.Minutes}m";
    }
}

public sealed record UsageSnapshot(DateTimeOffset ObservedAt, string Source, QuotaWindow? FiveHour, QuotaWindow? Weekly)
{
    public bool HasData => FiveHour is not null || Weekly is not null;
    public bool IsStale(DateTimeOffset now) => now - ObservedAt > TimeSpan.FromMinutes(3) || ObservedAt > now.AddMinutes(1);
    public static UsageSnapshot Unavailable(DateTimeOffset now) => new(now, "N/A", null, null);
}

public static class PaceCalculator
{
    // Compare quota consumed with time elapsed in the reported window. An estimate, not a forecast guarantee.
    public static string Describe(QuotaWindow? window, DateTimeOffset now)
    {
        if (window?.ResetsAt is not { } reset || window.HasExpired(now)) return "Pace unavailable";
        var elapsed = window.DurationMinutes - (reset - now).TotalMinutes;
        if (elapsed < 5 || elapsed > window.DurationMinutes) return "Learning pace";
        var projected = window.UsedPercent / elapsed * window.DurationMinutes;
        return projected > 105 ? "Above sustainable pace" : "On track";
    }
}
