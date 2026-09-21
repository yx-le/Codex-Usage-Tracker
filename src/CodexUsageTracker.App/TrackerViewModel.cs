using System.ComponentModel;

namespace CodexUsageTracker.App;

public sealed class TrackerViewModel : INotifyPropertyChanged
{
    public UsageSnapshot Snapshot { get; set; } = UsageSnapshot.Unavailable(DateTimeOffset.UtcNow);
    public string SourceDetail { get; set; } = "Connecting to Codex…";
    public bool Busy { get; set; }
    public bool CodexRunning { get; set; }
    public string StorageStatus { get; set; } = "";
    public TrackerSettings Settings { get; set; } = new();
    public QuotaStatus FiveStatus => QuotaStatusPolicy.Evaluate(Snapshot.FiveHour, Snapshot.IsStale(Now), Settings, Now);
    public QuotaStatus WeekStatus => QuotaStatusPolicy.Evaluate(Snapshot.Weekly, Snapshot.IsStale(Now), Settings, Now);
    public QuotaStatus OverallStatus => (QuotaStatus)Math.Max((int)FiveStatus, (int)WeekStatus);
    public Brush FiveBrush => StatusBrush(FiveStatus, "AccentBrush");
    public Brush WeekBrush => StatusBrush(WeekStatus, "WeekBrush");
    public Brush OverallBrush => StatusBrush(OverallStatus, "AccentBrush");
    public bool FiveExhausted => FiveStatus == QuotaStatus.Exhausted;
    public bool WeekExhausted => WeekStatus == QuotaStatus.Exhausted;
    public Brush FiveTrackBrush => FiveExhausted ? FiveBrush : (Brush)Application.Current.FindResource("TrackBrush");
    public Brush WeekTrackBrush => WeekExhausted ? WeekBrush : (Brush)Application.Current.FindResource("TrackBrush");
    public string WidgetStatus => OverallStatus switch
    {
        QuotaStatus.Exhausted => "EMPTY", QuotaStatus.Critical => "CRITICAL", QuotaStatus.Warning => "LOW",
        QuotaStatus.ResetDue => "RESET", QuotaStatus.Stale => "STALE", _ => Status
    };
    public string WarningText => string.Join("\n", new[] { WarningFor("5-hour", FiveStatus, Snapshot.FiveHour), WarningFor("Weekly", WeekStatus, Snapshot.Weekly) }.Where(value => value.Length > 0));
    public Visibility WarningVisibility => WarningText.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    public bool CanRefresh => !Busy;
    private static string WarningFor(string label, QuotaStatus status, QuotaWindow? quota) => status switch
    {
        QuotaStatus.Exhausted => $"{label} quota exhausted · waiting for reset",
        QuotaStatus.Critical => $"{label} quota critical · {quota!.RemainingPercent:0}% remaining",
        QuotaStatus.Warning => $"{label} quota low · {quota!.RemainingPercent:0}% remaining",
        QuotaStatus.ResetDue => $"{label} reset due · checking for fresh quota", _ => ""
    };
    private static Brush StatusBrush(QuotaStatus status, string healthy) => (Brush)Application.Current.FindResource(status switch
    {
        QuotaStatus.Exhausted or QuotaStatus.Critical => "CriticalBrush",
        QuotaStatus.Warning => "WarningBrush",
        QuotaStatus.Unknown or QuotaStatus.Stale or QuotaStatus.ResetDue => "MutedBrush", _ => healthy
    });
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
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}
