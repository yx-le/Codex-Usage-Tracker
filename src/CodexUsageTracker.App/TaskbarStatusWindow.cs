using System.Windows.Shell;

namespace CodexUsageTracker.App;

// A real Windows taskbar button: it never occupies desktop space. Restoring the
// button opens the existing details panel, rather than a second visible window.
internal sealed class TaskbarStatusWindow : Window
{
    private readonly TrackerController controller;
    public TaskbarStatusWindow(TrackerController controller)
    {
        this.controller = controller;
        Title = "Codex Usage Tracker"; Width = 1; Height = 1;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        Left = -32000; Top = -32000; ShowActivated = false; ShowInTaskbar = true;
        WindowState = WindowState.Minimized;
        TaskbarItemInfo = new TaskbarItemInfo();
        StateChanged += (_, _) =>
        {
            if (WindowState != WindowState.Minimized)
            { WindowState = WindowState.Minimized; controller.ShowDetails(); }
        };
        Closing += (_, args) => { args.Cancel = true; Hide(); };
    }
    public void UpdateStatus(ImageSource? icon)
    {
        var view = controller.ViewModel;
        Icon = icon;
        Title = $"5h {view.FiveRemaining} · Week {view.WeekRemaining} — Codex";
        TaskbarItemInfo.Description = $"{Title}\n{view.FiveReset}\n{view.WeekReset}\n{view.Status}";
        var state = view.OverallStatus;
        TaskbarItemInfo.ProgressState = state switch
        {
            QuotaStatus.Unknown or QuotaStatus.Stale or QuotaStatus.ResetDue => TaskbarItemProgressState.None,
            QuotaStatus.Critical or QuotaStatus.Exhausted => TaskbarItemProgressState.Error,
            QuotaStatus.Warning => TaskbarItemProgressState.Paused,
            _ => TaskbarItemProgressState.Normal
        };
        var remaining = new[] { view.Snapshot.FiveHour, view.Snapshot.Weekly }.Where(window => window is not null && !window.HasExpired(DateTimeOffset.UtcNow)).Select(window => window!.RemainingPercent).ToArray();
        TaskbarItemInfo.ProgressValue = state == QuotaStatus.Exhausted ? 1 : remaining.Length == 0 ? 0 : remaining.Min() / 100;
    }
}
