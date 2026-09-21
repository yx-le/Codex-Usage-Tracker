namespace CodexUsageTracker.Core;

public enum QuotaStatus { Unknown, Healthy, Stale, ResetDue, Warning, Critical, Exhausted }

public static class QuotaStatusPolicy
{
    public static QuotaStatus Evaluate(QuotaWindow? window, bool stale, TrackerSettings settings, DateTimeOffset now)
    {
        if (window is null) return QuotaStatus.Unknown;
        if (window.HasExpired(now)) return QuotaStatus.ResetDue;
        if (stale) return QuotaStatus.Stale;
        if (window.RemainingPercent <= 0) return QuotaStatus.Exhausted;
        if (window.RemainingPercent <= settings.CriticalPercent) return QuotaStatus.Critical;
        if (window.RemainingPercent <= settings.WarningPercent) return QuotaStatus.Warning;
        return QuotaStatus.Healthy;
    }
}
