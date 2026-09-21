using Microsoft.Win32;

namespace CodexUsageTracker.App;

public partial class App : Application
{
    private Mutex? instance;
    private TrackerController? controller;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length >= 2 && e.Args[0] == "--render-preview")
        {
            Directory.CreateDirectory(e.Args[1]);
            DispatcherUnhandledException += (_, error) =>
            {
                File.WriteAllText(Path.Combine(e.Args[1], "preview-error.txt"), error.Exception.ToString());
                error.Handled = true; Shutdown(1);
            };
        }
        instance = new Mutex(true, @"Local\CodexUsageTracker", out var created);
        if (!created) { Shutdown(); return; }
        try
        {
            var preview = e.Args.Length >= 2 && e.Args[0] == "--render-preview";
            controller = new TrackerController(preview ? Path.Combine(Path.GetFullPath(e.Args[1]), "preview-data") : null);
            if (preview) controller.RenderPreview(Path.GetFullPath(e.Args[1]), e.Args.Length >= 3 ? e.Args[2] : "Dark");
            else controller.Start();
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
    }

    protected override void OnExit(ExitEventArgs e)
    {
        controller?.Dispose(); instance?.Dispose(); base.OnExit(e);
    }
}
