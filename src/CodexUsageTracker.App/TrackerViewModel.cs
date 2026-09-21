using System.ComponentModel;

namespace CodexUsageTracker.App;

public sealed class TrackerViewModel : INotifyPropertyChanged
{
    public UsageSnapshot Snapshot { get; set; } = UsageSnapshot.Unavailable(DateTimeOffset.UtcNow);
    public string SourceDetail { get; set; } = "Connecting to Codex…";
    public bool Busy { get; set; }
    public bool CodexRunning { get; set; }
    public string StorageStatus { get; set; } = "";
    private DateTimeOffset Now => DateTimeOffset.UtcNow;
    private bool Valid(QuotaWindow? window) => window is not null && !window.HasExpired(Now);
    public string FiveRemaining => Valid(Snapshot.FiveHour) ? $"{Snapshot.FiveHour!.RemainingPercent:0}%" : "N/A";
    public string WeekRemaining => Valid(Snapshot.Weekly) ? $"{Snapshot.Weekly!.RemainingPercent:0}%" : "N/A";
    public double FiveValue => Valid(Snapshot.FiveHour) ? Snapshot.FiveHour!.RemainingPercent : 0;
    public double WeekValue => Valid(Snapshot.Weekly) ? Snapshot.Weekly!.RemainingPercent : 0;
    public string FiveUsed => Valid(Snapshot.FiveHour) ? $"{Snapshot.FiveHour!.UsedPercent:0}% used" : "Quota unavailable";
    public string WeekUsed => Valid(Snapshot.Weekly) ? $"{Snapshot.Weekly!.UsedPercent:0}% used" : "Quota unavailable";
    public string FiveReset => Snapshot.FiveHour?.Countdown(Now) ?? "Reset time unavailable";
    public string WeekReset => Snapshot.Weekly?.Countdown(Now) ?? "Reset time unavailable";
    public string FivePace => Snapshot.IsStale(Now) ? "Pace unavailable · stale data" : PaceCalculator.Describe(Snapshot.FiveHour, Now);
    public string WeekPace => Snapshot.IsStale(Now) ? "Pace unavailable · stale data" : PaceCalculator.Describe(Snapshot.Weekly, Now);
    public string Status => !Snapshot.HasData ? "N/A" : Snapshot.Source.StartsWith("Preview", StringComparison.Ordinal) ? "DEMO" : Snapshot.IsStale(Now) ? "STALE" : Snapshot.FiveHour?.HasExpired(Now) == true || Snapshot.Weekly?.HasExpired(Now) == true ? "RESET DUE" : Snapshot.Source == "Codex app-server" ? "LIVE" : "LOCAL";
    public string LastUpdated => !Snapshot.HasData ? "No quota reading yet" : $"Last reading {Snapshot.ObservedAt.ToLocalTime():ddd HH:mm:ss} · {(int)Math.Max(0, (Now - Snapshot.ObservedAt).TotalMinutes)}m ago";
    public string Connection => CodexRunning ? "Codex is running" : "Codex is not running";
    public string RefreshLabel => Busy ? "Updating…" : "Refresh";
    public string WidgetTooltip => $"Codex · 5-hour {FiveRemaining} · weekly {WeekRemaining} remaining\n{Status} · {LastUpdated}\nClick for details · drag to move";
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}
