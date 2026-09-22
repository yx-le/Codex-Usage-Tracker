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
        Icon = CreateQuotaIcon(view);
        Title = $"Codex · 5h {view.FiveRemaining} · Week {view.WeekRemaining}";
        TaskbarItemInfo.Description = $"{Title}\n{view.FiveReset}\n{view.WeekReset}\n{view.Status}";
        var state = view.OverallStatus;
        TaskbarItemInfo.ProgressState = state switch
        {
            QuotaStatus.Unknown or QuotaStatus.Stale or QuotaStatus.ResetDue => TaskbarItemProgressState.None,
            QuotaStatus.Critical or QuotaStatus.Exhausted => TaskbarItemProgressState.Error,
            QuotaStatus.Warning => TaskbarItemProgressState.Paused,
            _ => TaskbarItemProgressState.None
        };
        var remaining = new[] { view.Snapshot.FiveHour, view.Snapshot.Weekly }.Where(window => window is not null && !window.HasExpired(DateTimeOffset.UtcNow)).Select(window => window!.RemainingPercent).ToArray();
        TaskbarItemInfo.ProgressValue = state == QuotaStatus.Exhausted ? 1 : remaining.Length == 0 ? 0 : remaining.Min() / 100;
    }
    internal static System.Windows.Media.Imaging.RenderTargetBitmap CreateQuotaIcon(TrackerViewModel view)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(25, 34, 44)), null, new Rect(0, 0, 32, 32), 7, 7);
            foreach (var (value, state, y, healthy) in new[] {
                (view.FiveValue, view.FiveStatus, 8d, Color.FromRgb(121, 200, 182)),
                (view.WeekValue, view.WeekStatus, 20d, Color.FromRgb(168, 172, 216)) })
            {
                var color = state switch { QuotaStatus.Warning => Colors.Orange, QuotaStatus.Critical or QuotaStatus.Exhausted => Colors.LightCoral, QuotaStatus.Stale or QuotaStatus.Unknown or QuotaStatus.ResetDue => Colors.SlateGray, _ => healthy };
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(65, 77, 90)), null, new Rect(5, y, 22, 4), 2, 2);
                var length = state == QuotaStatus.Exhausted ? 22 : Math.Clamp(value, 0, 100) / 100 * 22;
                if (length > 0) dc.DrawRoundedRectangle(new SolidColorBrush(color), null, new Rect(5, y, length, 4), 2, 2);
            }
        }
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }
}
