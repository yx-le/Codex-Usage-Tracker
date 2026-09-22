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
    private readonly AsyncUsageRepository repository;
    private int historyVersion;
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
    private readonly EdgeBarWindow edgeBar;
    private readonly TaskbarStatusWindow taskbar;
    private bool edgeAnchor;
    private readonly DetailsWindow details;
    private DateTimeOffset lastAttempt = DateTimeOffset.MinValue;
    private bool disposed, lastRunning;
    private Drawing.Icon? ownedIcon;
    public TrackerSettings Settings { get; private set; }
    public TrackerViewModel ViewModel { get; } = new();

    public TrackerController(string? dataDirectory = null)
    {
        dataDirectory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexUsageTracker");
        settingsPath = Path.Combine(dataDirectory, "settings.json");
        Settings = TrackerSettings.Load(settingsPath); App.ApplyTheme(Settings.Theme);
        ViewModel.Settings = Settings;
        repository = new AsyncUsageRepository(Path.Combine(dataDirectory, "usage.db"));
        widget = new WidgetWindow(this); details = new DetailsWindow(this); edgeBar = new EdgeBarWindow(this); taskbar = new TaskbarStatusWindow(this);
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
        UpdateTray();
    }

    public void Start() { timer.Start(); _ = UpdateHistoryAsync(); OnTick(null, EventArgs.Empty); }
    private async void OnTick(object? sender, EventArgs e)
    {
        if (disposed) return;
        // The short-lived quota reader is not allowed to keep the widget visible by itself.
        if (!ViewModel.Busy) ViewModel.CodexRunning = CodexLocator.IsRunning();
        var running = ViewModel.CodexRunning;
        if (Settings.FloatingWidget && (!Settings.OnlyWhileCodexRunning || running))
        { if (!widget.IsVisible) widget.Show(); }
        else widget.Hide();
        if (Settings.EdgeBar && (!Settings.OnlyWhileCodexRunning || running))
        { if (!edgeBar.IsVisible) { edgeBar.Place(); edgeBar.Show(); edgeBar.Place(); } }
        else edgeBar.Hide();
        if (Settings.TaskbarStatus && (!Settings.OnlyWhileCodexRunning || running)) { if (!taskbar.IsVisible) taskbar.Show(); } else taskbar.Hide();
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
            ViewModel.Notify();
            foreach (var message in alerts.Evaluate(result.Item1, Settings, DateTimeOffset.UtcNow))
                tray.ShowBalloonTip(7000, "Codex quota running low", message, Forms.ToolTipIcon.Warning);
            UpdateTray();
            await UpdateHistoryAsync(result.Item1);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        finally { ViewModel.Busy = false; ViewModel.Notify(); }
    }

    private void UpdateTray()
    {
        var state = $"{ViewModel.FiveValue}|{ViewModel.WeekValue}|{ViewModel.OverallStatus}|{ViewModel.Status}|{Settings.Theme}|{App.IsDarkTheme(Settings.Theme)}|{Settings.TrayPercentage}|{Settings.TrayQuota}|{ViewModel.FiveStatus}|{ViewModel.WeekStatus}";
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
        taskbar.UpdateStatus(System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(next.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions()));
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
    public void ToggleDetails(bool fromEdge = false) { edgeAnchor = fromEdge; if (details.IsVisible) details.Hide(); else ShowDetails(); }
    public void ToggleEdgeDetails() => ToggleDetails(fromEdge: true);
    public void ShowDetails()
    {
        details.ShowUsagePage();
        PositionPanel(); details.Show(); PositionPanel(); details.Activate();
        if (timer.IsEnabled) _ = UpdateHistoryAsync();
    }
    public void RepositionPanel() { if (details.IsVisible) PositionPanel(); }
    private void PositionPanel()
    {
        Window anchorWindow = edgeBar.IsVisible && (edgeAnchor || !widget.IsVisible) ? edgeBar : widget;
        var dpi = VisualTreeHelper.GetDpi(anchorWindow);
        var handle = new System.Windows.Interop.WindowInteropHelper(anchorWindow).Handle;
        var screen = handle != IntPtr.Zero ? Forms.Screen.FromHandle(handle) : Forms.Screen.PrimaryScreen!;
        var area = screen.WorkingArea;
        var work = new LayoutRect(area.Left / dpi.DpiScaleX, area.Top / dpi.DpiScaleY, area.Width / dpi.DpiScaleX, area.Height / dpi.DpiScaleY);
        var anchor = new LayoutRect(anchorWindow.Left, anchorWindow.Top, anchorWindow.Width, anchorWindow.Height);
        if (!anchorWindow.IsVisible) anchor = new LayoutRect(work.Right - 96, work.Bottom - 96, 96, 96);
        var placement = PanelPlacement.Beside(work, anchor, 420, 790);
        details.Width = placement.Width; details.Height = placement.Height;
        details.Left = placement.Left; details.Top = placement.Top;
    }
    public void ShowSettings() { if (!details.IsVisible) ShowDetails(); details.ShowSettingsPage(); details.Activate(); }
    public void ShowUsagePage() => details.ShowUsagePage();
    public void SaveSettings()
    {
        try { Settings.Save(settingsPath); App.ApplyTheme(Settings.Theme); GlassWindow.Apply(details, App.IsDarkTheme(Settings.Theme)); widget.Ring.InvalidateVisual(); details.Chart.InvalidateVisual(); edgeBar.Place(); ViewModel.Notify(); UpdateTray(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { MessageBox.Show("Settings could not be saved. Check folder access.", "Codex Usage Tracker"); }
    }
    public bool TrySaveSettings(TrackerSettings candidate, out string error)
    {
        try { candidate.Save(settingsPath); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { error = "Settings could not be saved. Your edits are still here; check folder access and try again."; return false; }
        Settings = candidate; ViewModel.Settings = candidate;
        App.ApplyTheme(Settings.Theme); GlassWindow.Apply(details, App.IsDarkTheme(Settings.Theme));
        widget.Ring.InvalidateVisual(); details.Chart.InvalidateVisual(); edgeBar.Place(); ViewModel.Notify(); UpdateTray();
        error = ""; return true;
    }
    public Task ClearHistoryAsync() => UpdateHistoryAsync(clear: true);
    private async Task UpdateHistoryAsync(UsageSnapshot? snapshot = null, bool clear = false)
    {
        var version = ++historyVersion;
        try
        {
            var history = await (clear ? repository.ClearAsync(lifetime.Token)
                : snapshot is not null ? repository.SaveAndReadAsync(snapshot, lifetime.Token)
                : repository.ReadAsync(lifetime.Token));
            if (disposed || version != historyVersion) return;
            details.SetHistory(history); ViewModel.StorageStatus = ""; ViewModel.Notify();
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            if (!disposed && version == historyVersion)
            { ViewModel.StorageStatus = "History is temporarily unavailable. Check folder access or try again shortly."; ViewModel.Notify(); }
            if (clear) throw;
        }
    }
    private void OnSystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) => Application.Current.Dispatcher.BeginInvoke(() => { App.ApplyTheme(Settings.Theme); GlassWindow.Apply(details, App.IsDarkTheme(Settings.Theme)); widget.Ring.InvalidateVisual(); details.Chart.InvalidateVisual(); });
    private void OnDisplayChanged(object? sender, EventArgs e) => Application.Current.Dispatcher.BeginInvoke(() => { widget.ClampPosition(); edgeBar.Place(); RepositionPanel(); });

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
            PreviewScene.Save(Path.Combine(directory, "widget-context.png"), widget, false);
            PreviewScene.Save(Path.Combine(directory, "details-context.png"), details, true);
            foreach (var edge in new[] { "Top", "Left", "Right" }) { Settings.Edge = edge; edgeBar.Place(); edgeBar.Show(); edgeBar.Place(); Capture("edge-" + edge, edgeBar); }
            edgeBar.Hide(); Settings.Edge = "Top";
            var windowCount = Application.Current.Windows.Count;
            details.ShowSettingsPage(); Capture("settings", details);
            var original = Settings;
            var candidate = original.Copy(); candidate.WarningPercent = 44;
            var blockedSave = settingsPath + ".tmp";
            Directory.CreateDirectory(blockedSave);
            try
            {
                if (TrySaveSettings(candidate, out _) || !ReferenceEquals(Settings, original) || !details.IsSettingsPage)
                    throw new InvalidOperationException("A failed save must preserve settings and navigation.");
            }
            finally { Directory.Delete(blockedSave); }
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
            var fresh = ViewModel.Snapshot;
            ViewModel.Snapshot = fresh with { ObservedAt = now.AddMinutes(-5), FiveHour = new(27, 300, now.AddHours(2)) };
            ViewModel.Notify(); Capture("widget-stale", widget);
            ViewModel.Snapshot = fresh; ViewModel.Notify();
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
