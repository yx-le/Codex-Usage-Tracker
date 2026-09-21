using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace CodexUsageTracker.App;

public sealed class TrackerController : IDisposable
{
    private readonly string settingsPath;
    private readonly UsageRepository repository;
    private readonly CancellationTokenSource lifetime = new();
    private readonly AppServerSource server = new();
    private readonly AlertPolicy alerts = new();
    private readonly Forms.NotifyIcon tray;
    private readonly DispatcherTimer timer;
    private readonly WidgetWindow widget;
    private readonly DetailsWindow details;
    private DateTimeOffset lastAttempt = DateTimeOffset.MinValue;
    private bool disposed, lastRunning;
    private Drawing.Icon? ownedIcon;
    public TrackerSettings Settings { get; }
    public TrackerViewModel ViewModel { get; } = new();

    public TrackerController(string? dataDirectory = null)
    {
        dataDirectory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexUsageTracker");
        settingsPath = Path.Combine(dataDirectory, "settings.json");
        Settings = TrackerSettings.Load(settingsPath); App.ApplyTheme(Settings.Theme);
        repository = new UsageRepository(Path.Combine(dataDirectory, "usage.db"));
        widget = new WidgetWindow(this); details = new DetailsWindow(this);
        tray = new Forms.NotifyIcon { Text = "Codex Usage Tracker", Visible = true, Icon = Drawing.SystemIcons.Information };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show quota details", null, (_, _) => ShowDetails());
        menu.Items.Add("Refresh now", null, async (_, _) => await RefreshAsync());
        menu.Items.Add("Settings", null, (_, _) => ShowSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Application.Current.Shutdown());
        tray.ContextMenuStrip = menu;
        tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) ToggleDetails(); };
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += OnTick;
        SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        UpdateHistory();
    }

    public void Start() { timer.Start(); OnTick(null, EventArgs.Empty); }
    private async void OnTick(object? sender, EventArgs e)
    {
        if (disposed) return;
        // The short-lived quota reader is not allowed to keep the widget visible by itself.
        if (!ViewModel.Busy) ViewModel.CodexRunning = CodexLocator.IsRunning();
        var running = ViewModel.CodexRunning;
        if (Settings.FloatingWidget && (!Settings.OnlyWhileCodexRunning || running))
        { if (!widget.IsVisible) widget.Show(); }
        else widget.Hide();
        if (Settings.OnlyWhileCodexRunning && !running && lastRunning) details.Hide();
        var justStarted = running && !lastRunning; lastRunning = running;
        ViewModel.Notify();
        if (running && (justStarted || DateTimeOffset.UtcNow - lastAttempt > TimeSpan.FromSeconds(60))) await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (ViewModel.Busy || disposed) return;
        ViewModel.Busy = true; ViewModel.Notify(); lastAttempt = DateTimeOffset.UtcNow;
        try
        {
            var settingsExecutable = Settings.CodexExecutable;
            var result = await Task.Run(async () =>
            {
                UsageSnapshot? snapshot = null;
                var reason = "Codex executable not found. Select codex.exe in Settings.";
                try
                {
                    var executable = CodexLocator.Find(settingsExecutable);
                    if (executable is not null)
                    {
                        snapshot = await server.ReadAsync(executable, lifetime.Token);
                        reason = snapshot.HasData ? "" : "Codex returned no supported five-hour or weekly quota.";
                    }
                }
                catch (OperationCanceledException) when (!lifetime.IsCancellationRequested) { reason = "Codex quota request timed out."; }
                catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception or System.Text.Json.JsonException or InvalidOperationException or UnauthorizedAccessException)
                { reason = "App-server unavailable. Check Codex sign-in and executable in Settings."; }
                if (snapshot?.HasData == true) return (snapshot, detail: "Codex app-server · current quota reading");
                var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
                try { snapshot = new LocalCodexSource().Read(codexHome, lifetime.Token); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
                return (snapshot ?? UsageSnapshot.Unavailable(DateTimeOffset.UtcNow), detail: snapshot is not null ? $"Local fallback · cached quota. {reason}" : $"N/A · {reason}");
            }, lifetime.Token);
            if (disposed) return;
            ViewModel.Snapshot = result.Item1; ViewModel.SourceDetail = result.detail;
            try { repository.Save(result.Item1); repository.Prune(DateTimeOffset.UtcNow); ViewModel.StorageStatus = ""; UpdateHistory(); }
            catch (Exception e) when (e is IOException or Microsoft.Data.Sqlite.SqliteException) { ViewModel.StorageStatus = "History could not be saved. Check available disk space and folder access."; }
            foreach (var message in alerts.Evaluate(result.Item1, Settings, DateTimeOffset.UtcNow))
                tray.ShowBalloonTip(7000, "Codex quota running low", message, Forms.ToolTipIcon.Warning);
            UpdateTray();
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        finally { ViewModel.Busy = false; ViewModel.Notify(); }
    }

    private void UpdateTray()
    {
        tray.Text = $"Codex · 5h {ViewModel.FiveRemaining} · week {ViewModel.WeekRemaining} · {ViewModel.Status}";
        using var bitmap = new Drawing.Bitmap(32, 32);
        using var graphics = Drawing.Graphics.FromImage(bitmap);
        graphics.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var background = new Drawing.Pen(Drawing.Color.FromArgb(100, 120, 135), 3);
        using var five = new Drawing.Pen(Drawing.Color.FromArgb(45, 195, 150), 3);
        using var week = new Drawing.Pen(Drawing.Color.FromArgb(150, 170, 255), 2);
        graphics.DrawEllipse(background, 3, 3, 26, 26);
        if (ViewModel.FiveValue > 0) graphics.DrawArc(five, 3, 3, 26, 26, -90, (float)(ViewModel.FiveValue * 3.6));
        if (ViewModel.WeekValue > 0) graphics.DrawArc(week, 8, 8, 16, 16, -90, (float)(ViewModel.WeekValue * 3.6));
        var handle = bitmap.GetHicon();
        Drawing.Icon next;
        using (var borrowed = Drawing.Icon.FromHandle(handle)) next = (Drawing.Icon)borrowed.Clone();
        DestroyIcon(handle);
        tray.Icon = next; ownedIcon?.Dispose(); ownedIcon = next;
    }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(IntPtr handle);
    public void ToggleDetails() { if (details.IsVisible) details.Hide(); else ShowDetails(); }
    public void ShowDetails()
    {
        var work = SystemParameters.WorkArea;
        details.Height = Math.Min(790, work.Height);
        details.Left = Math.Clamp(widget.Left - details.Width - 8, work.Left, Math.Max(work.Left, work.Right - details.Width));
        details.Top = Math.Clamp(widget.Top - details.Height + 96, work.Top, Math.Max(work.Top, work.Bottom - details.Height));
        UpdateHistory(); details.Show(); details.Activate();
    }
    public void ShowSettings() { var dialog = new SettingsWindow(this); if (details.IsVisible) dialog.Owner = details; dialog.ShowDialog(); }
    public void SaveSettings()
    {
        try { Settings.Save(settingsPath); App.ApplyTheme(Settings.Theme); widget.Ring.InvalidateVisual(); details.Chart.InvalidateVisual(); ViewModel.Notify(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { MessageBox.Show("Settings could not be saved. Check folder access.", "Codex Usage Tracker"); }
    }
    public void ClearHistory() { repository.Clear(); UpdateHistory(); }
    private void UpdateHistory()
    {
        try { details.SetHistory(repository.ReadHistory(DateTimeOffset.UtcNow)); }
        catch (Microsoft.Data.Sqlite.SqliteException) { ViewModel.StorageStatus = "History is temporarily unavailable."; }
    }
    private void OnSystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) => Application.Current.Dispatcher.BeginInvoke(() => { App.ApplyTheme(Settings.Theme); widget.Ring.InvalidateVisual(); details.Chart.InvalidateVisual(); });
    private void OnDisplayChanged(object? sender, EventArgs e) => Application.Current.Dispatcher.BeginInvoke(widget.ClampPosition);

    public void RenderPreview(string directory, string theme = "Dark")
    {
        Directory.CreateDirectory(directory);
        Settings.Theme = theme; App.ApplyTheme(theme);
        Settings.OnlyWhileCodexRunning = false;
        var now = DateTimeOffset.UtcNow;
        ViewModel.Snapshot = new(now, "Preview · synthetic data", new(27, 300, now.AddHours(3)), new(58, 10080, now.AddDays(3)));
        ViewModel.SourceDetail = "PREVIEW · synthetic quota for layout verification";
        ViewModel.Notify(); widget.Show(); ShowDetails();
        details.SetHistory(Enumerable.Range(0, 80).Select(i => new UsageSnapshot(now.AddDays(-7).AddHours(i * 168d / 79), "Preview", new(10 + i % 20 * 3, 300, now.AddHours(3)), new(i * 58d / 79, 10080, now.AddDays(3)))).ToArray());
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var settingsWindow = new SettingsWindow(this); settingsWindow.Show(); settingsWindow.UpdateLayout();
            foreach (var (name, target) in new[] { ("widget", (Window)widget), ("details", (Window)details), ("settings", (Window)settingsWindow) })
            {
                target.UpdateLayout();
                var image = new System.Windows.Media.Imaging.RenderTargetBitmap((int)target.ActualWidth, (int)target.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                image.Render(target);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
                using var file = File.Create(Path.Combine(directory, name + ".png")); encoder.Save(file);
            }
            Application.Current.Shutdown();
        }, DispatcherPriority.ApplicationIdle);
    }

    public void Dispose()
    {
        if (disposed) return; disposed = true; lifetime.Cancel(); timer.Stop();
        SystemEvents.UserPreferenceChanged -= OnSystemPreferenceChanged; SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); ownedIcon?.Dispose();
    }
}
