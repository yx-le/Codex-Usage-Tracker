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
    private readonly ResetRefreshSchedule resetRefresh = new();
    private string lastTrayState = "";
    private readonly Forms.ToolStripMenuItem trayQuota;
    private readonly Forms.ToolStripMenuItem trayWeek;
    private readonly Forms.ToolStripMenuItem trayFloating;
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
        ViewModel.Settings = Settings;
        repository = new UsageRepository(Path.Combine(dataDirectory, "usage.db"));
        widget = new WidgetWindow(this); details = new DetailsWindow(this);
        tray = new Forms.NotifyIcon { Text = "Codex Usage Tracker", Visible = true, Icon = Drawing.SystemIcons.Information };
        var menu = new RoundedTrayMenu
        {
            Renderer = new TrayMenuRenderer(), ShowImageMargin = false,
            Font = new Drawing.Font("Segoe UI", 9), Padding = new Forms.Padding(8),
            MinimumSize = new Drawing.Size(260, 0)
        };
        menu.Items.Add(new Forms.ToolStripMenuItem("Quota remaining") { Enabled = false });
        trayQuota = new Forms.ToolStripMenuItem("5-hour", null, (_, _) => ShowDetails());
        trayWeek = new Forms.ToolStripMenuItem("Weekly", null, (_, _) => ShowDetails());
        menu.Items.Add(trayQuota); menu.Items.Add(trayWeek);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Open usage details", null, (_, _) => ShowDetails());
        menu.Items.Add("Refresh now", null, async (_, _) => await RefreshAsync());
        menu.Items.Add("Settings", null, (_, _) => ShowSettings());
        trayFloating = new Forms.ToolStripMenuItem("Hide floating circle", null, (_, _) =>
        { Settings.FloatingWidget = !Settings.FloatingWidget; SaveSettings(); });
        menu.Items.Add(trayFloating);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Application.Current.Shutdown());
        tray.ContextMenuStrip = menu;
        foreach (Forms.ToolStripItem item in menu.Items)
        {
            item.Padding = item is Forms.ToolStripSeparator ? new Forms.Padding(0, 4, 0, 4) : new Forms.Padding(10, 5, 10, 5);
            var preferred = item.GetPreferredSize(Drawing.Size.Empty);
            item.AutoSize = false;
            item.Size = new Drawing.Size(244 * menu.DeviceDpi / 96, preferred.Height);
        }
        menu.Opening += (_, _) => UpdateTrayMenu();
        tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) ToggleDetails(); };
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += OnTick;
        SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        UpdateHistory();
        UpdateTray();
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
        UpdateTray();
        if (ViewModel.Busy) return;
        var now = DateTimeOffset.UtcNow;
        var resetDue = resetRefresh.ShouldRefresh(ViewModel.Snapshot, now);
        if (resetDue || (running && (justStarted || now - lastAttempt > TimeSpan.FromSeconds(60)))) await RefreshAsync();
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
        var state = $"{ViewModel.FiveValue}|{ViewModel.WeekValue}|{ViewModel.OverallStatus}|{ViewModel.Status}|{Settings.Theme}|{App.IsDarkTheme(Settings.Theme)}";
        if (state == lastTrayState) return;
        lastTrayState = state;
        tray.Text = $"Codex · 5h {ViewModel.FiveRemaining} · week {ViewModel.WeekRemaining} · {ViewModel.Status}";
        using var bitmap = new Drawing.Bitmap(32, 32);
        using var graphics = Drawing.Graphics.FromImage(bitmap);
        graphics.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var disc = new Drawing.SolidBrush(Drawing.Color.FromArgb(22, 32, 42));
        graphics.FillEllipse(disc, 1, 1, 30, 30);
        using var background = new Drawing.Pen(Drawing.Color.FromArgb(100, 120, 135), 3);
        var color = ((SolidColorBrush)ViewModel.OverallBrush).Color;
        using var ring = new Drawing.Pen(Drawing.Color.FromArgb(color.R, color.G, color.B), 4);
        graphics.DrawEllipse(background, 3, 3, 26, 26);
        var values = new[] { ViewModel.Snapshot.FiveHour, ViewModel.Snapshot.Weekly }.Where(window => window is not null && !window.HasExpired(DateTimeOffset.UtcNow)).Select(window => window!.RemainingPercent).ToArray();
        var remaining = values.Length > 0 ? values.Min() : 0;
        if (ViewModel.OverallStatus == QuotaStatus.Exhausted) graphics.DrawEllipse(ring, 3, 3, 26, 26);
        else if (remaining > 0) graphics.DrawArc(ring, 3, 3, 26, 26, -90, (float)(remaining * 3.6));
        var label = ViewModel.OverallStatus switch
        {
            QuotaStatus.Exhausted => "0", QuotaStatus.Critical or QuotaStatus.Warning => "!",
            QuotaStatus.Unknown or QuotaStatus.ResetDue or QuotaStatus.Stale => "?", _ => "✓"
        };
        using var font = new Drawing.Font("Segoe UI", 15, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel);
        using var textBrush = new Drawing.SolidBrush(Drawing.Color.White);
        using var format = new Drawing.StringFormat { Alignment = Drawing.StringAlignment.Center, LineAlignment = Drawing.StringAlignment.Center };
        graphics.DrawString(label, font, textBrush, new Drawing.RectangleF(2, 2, 28, 28), format);
        var handle = bitmap.GetHicon();
        Drawing.Icon next;
        using (var borrowed = Drawing.Icon.FromHandle(handle)) next = (Drawing.Icon)borrowed.Clone();
        DestroyIcon(handle);
        tray.Icon = next; ownedIcon?.Dispose(); ownedIcon = next;
    }
    private void UpdateTrayMenu()
    {
        trayQuota.Tag = ViewModel.FiveRemaining;
        trayWeek.Tag = ViewModel.WeekRemaining;
        trayFloating.Text = Settings.FloatingWidget ? "Hide floating circle" : "Show floating circle";
        if (tray.ContextMenuStrip is { } menu)
        { menu.BackColor = TrayMenuRenderer.Background; menu.ForeColor = TrayMenuRenderer.Foreground; }
    }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(IntPtr handle);
    public void ToggleDetails() { if (details.IsVisible) details.Hide(); else ShowDetails(); }
    public void ShowDetails()
    {
        details.ShowUsagePage();
        PositionPanel(); UpdateHistory(); details.Show(); PositionPanel(); details.Activate();
    }
    public void RepositionPanel() { if (details.IsVisible) PositionPanel(); }
    private void PositionPanel()
    {
        var dpi = VisualTreeHelper.GetDpi(widget);
        var screen = Forms.Screen.FromPoint(new Drawing.Point((int)((widget.Left + 48) * dpi.DpiScaleX), (int)((widget.Top + 48) * dpi.DpiScaleY)));
        var area = screen.WorkingArea;
        var work = new LayoutRect(area.Left / dpi.DpiScaleX, area.Top / dpi.DpiScaleY, area.Width / dpi.DpiScaleX, area.Height / dpi.DpiScaleY);
        var anchor = new LayoutRect(widget.Left, widget.Top, widget.Width, widget.Height);
        if (!widget.IsVisible) anchor = new LayoutRect(work.Right - 96, work.Bottom - 96, 96, 96);
        var placement = PanelPlacement.Beside(work, anchor, 420, 790);
        details.Width = placement.Width; details.Height = placement.Height;
        details.Left = placement.Left; details.Top = placement.Top;
    }
    public void ShowSettings() { if (!details.IsVisible) ShowDetails(); details.ShowSettingsPage(); details.Activate(); }
    public void ShowUsagePage() => details.ShowUsagePage();
    public void SaveSettings()
    {
        try { Settings.Save(settingsPath); App.ApplyTheme(Settings.Theme); GlassWindow.Apply(details, App.IsDarkTheme(Settings.Theme)); widget.Ring.InvalidateVisual(); details.Chart.InvalidateVisual(); ViewModel.Notify(); UpdateTray(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { MessageBox.Show("Settings could not be saved. Check folder access.", "Codex Usage Tracker"); }
    }
    public void ClearHistory() { repository.Clear(); UpdateHistory(); }
    private void UpdateHistory()
    {
        try { details.SetHistory(repository.ReadHistory(DateTimeOffset.UtcNow)); }
        catch (Microsoft.Data.Sqlite.SqliteException) { ViewModel.StorageStatus = "History is temporarily unavailable."; }
    }
    private void OnSystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) => Application.Current.Dispatcher.BeginInvoke(() => { App.ApplyTheme(Settings.Theme); GlassWindow.Apply(details, App.IsDarkTheme(Settings.Theme)); widget.Ring.InvalidateVisual(); details.Chart.InvalidateVisual(); });
    private void OnDisplayChanged(object? sender, EventArgs e) => Application.Current.Dispatcher.BeginInvoke(() => { widget.ClampPosition(); RepositionPanel(); });

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
            void Capture(string name, Window target)
            {
                target.UpdateLayout();
                var image = new System.Windows.Media.Imaging.RenderTargetBitmap((int)target.ActualWidth, (int)target.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                image.Render(target);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
                using var file = File.Create(Path.Combine(directory, name + ".png")); encoder.Save(file);
            }
            Capture("widget", widget); Capture("details", details);
            var windowCount = Application.Current.Windows.Count;
            details.ShowSettingsPage(); Capture("settings", details);
            if (!details.IsSettingsPage || Application.Current.Windows.Count != windowCount)
                throw new InvalidOperationException("Settings must replace usage inside the same window.");
            details.ShowUsagePage();
            if (details.IsSettingsPage) throw new InvalidOperationException("Back navigation failed.");
            foreach (var (name, used) in new[] { ("warning", 78d), ("critical", 94d), ("exhausted", 100d) })
            {
                ViewModel.Snapshot = new(now, "Preview · synthetic data", new(used, 300, now.AddHours(2)), new(16, 10080, now.AddDays(6)));
                ViewModel.Notify(); Capture("widget-" + name, widget); Capture("details-" + name, details);
            }
            UpdateTray(); UpdateTrayMenu();
            if (tray.ContextMenuStrip is { } menu)
            {
                menu.CreateControl(); menu.Size = menu.GetPreferredSize(Drawing.Size.Empty);
                using var bitmap = new Drawing.Bitmap(menu.Width, menu.Height);
                menu.DrawToBitmap(bitmap, new Drawing.Rectangle(Drawing.Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(directory, "tray-menu.png"), Drawing.Imaging.ImageFormat.Png);
            }
            using (var iconBitmap = ownedIcon?.ToBitmap())
                iconBitmap?.Save(Path.Combine(directory, "tray-icon.png"), Drawing.Imaging.ImageFormat.Png);
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
