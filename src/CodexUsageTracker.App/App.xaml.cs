using Microsoft.Win32;

namespace CodexUsageTracker.App;

public partial class App : Application
{
    private Mutex? instance;
    private TrackerController? controller;
    private CodexStartupWatcher? watcher;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--enable-startup"))
        {
            try { StartupRegistration.SetEnabled(true); Shutdown(0); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException) { Shutdown(1); }
            return;
        }
        if (e.Args.Length >= 2 && e.Args[0] == "--render-preview")
        {
            Directory.CreateDirectory(e.Args[1]);
            DispatcherUnhandledException += (_, error) =>
            {
                File.WriteAllText(Path.Combine(e.Args[1], "preview-error.txt"), error.Exception.ToString());
                error.Handled = true; Shutdown(1);
            };
        }
        var watchMode = e.Args.Contains("--watch-codex");
        instance = new Mutex(true, watchMode ? @"Local\CodexUsageTracker.Watcher" : @"Local\CodexUsageTracker", out var created);
        if (!created) { Shutdown(); return; }
        if (watchMode) { watcher = new CodexStartupWatcher(); return; }
        try
        {
            var preview = e.Args.Length >= 2 && e.Args[0] == "--render-preview";
            controller = new TrackerController(preview ? Path.Combine(Path.GetFullPath(e.Args[1]), "preview-data") : null);
            if (preview) controller.RenderPreview(Path.GetFullPath(e.Args[1]), e.Args.Length >= 3 ? e.Args[2] : "Dark");
            else { controller.Start(); if (StartupRegistration.IsEnabled) StartupRegistration.EnsureWatcher(); }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            if (e.Args.Length >= 2 && e.Args[0] == "--render-preview")
            {
                File.WriteAllText(Path.Combine(e.Args[1], "preview-error.txt"), exception.ToString());
                Shutdown(1); return;
            }
            MessageBox.Show("The tracker cannot access its local settings or history. Check access to %LOCALAPPDATA%\\CodexUsageTracker.", "Codex Usage Tracker");
            Shutdown(1);
        }
    }

    public static bool IsDarkTheme(string choice) => choice == "Dark" ||
        (choice == "System" && (int?)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) == 0);

    public static void ApplyTheme(string choice)
    {
        var dark = IsDarkTheme(choice);
        var palette = dark
            ? new[] { "#00111820", "#99141E28", "#E61A2631", "#CC283A47", "#D923313D", "#B53A4D5A", "#FFF7FAFC", "#FFEAF1F6", "#597B8D99", "#52637682", "#FF62E8C1", "#FF071A17", "#FFA8B7FF", "#FFFFB36B" }
            : new[] { "#00F5F9FC", "#99EEF5F8", "#F7FFFFFF", "#CCFFFFFF", "#EFFFFFFF", "#E5DCEAF0", "#FF102938", "#FF213C4B", "#526F8795", "#426D8795", "#FF007F68", "#FFFFFFFF", "#FF515EBD", "#FFC56016" };
        var keys = new[] { "BackgroundBrush", "GlassBrush", "GlassStrongBrush", "CardBrush", "InputBrush", "ButtonHoverBrush", "TextBrush", "MutedBrush", "BorderBrush", "TrackBrush", "AccentBrush", "AccentTextBrush", "WeekBrush", "DangerBrush" };
        for (var i = 0; i < keys.Length; i++) Current.Resources[keys[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(palette[i]));
        Current.Resources["WarningBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#FFC05C" : "#974B00"));
        Current.Resources["CriticalBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#FF718C" : "#AE1638"));
        Current.Resources["EdgeGlassBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#40141E28" : "#40FFFFFF"));
        Current.Resources["EdgeFiveAccent"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#79C8B6" : "#34786A"));
        Current.Resources["EdgeWeekAccent"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#A8ACD8" : "#656995"));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        watcher?.Dispose(); controller?.Dispose(); instance?.Dispose(); base.OnExit(e);
    }
}
