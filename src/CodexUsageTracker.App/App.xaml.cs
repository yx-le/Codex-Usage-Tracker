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

    public static void ApplyTheme(string choice)
    {
        var dark = choice == "Dark" || (choice == "System" && (int?)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) == 0);
        var palette = dark
            ? new[] { "#111B24", "#1A2833", "#F0F5F7", "#A5B5C2", "#30424F", "#4AD9B3", "#99ACFF" }
            : new[] { "#F5F8FA", "#FFFFFF", "#142C3B", "#526A7A", "#DAE4EA", "#087D63", "#5262BC" };
        var keys = new[] { "BackgroundBrush", "CardBrush", "TextBrush", "MutedBrush", "BorderBrush", "AccentBrush", "WeekBrush" };
        for (var i = 0; i < keys.Length; i++) Current.Resources[keys[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(palette[i]));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        controller?.Dispose(); instance?.Dispose(); base.OnExit(e);
    }
}
