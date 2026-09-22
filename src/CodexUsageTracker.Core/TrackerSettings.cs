using System.Text.Json;

namespace CodexUsageTracker.Core;

public sealed class TrackerSettings
{
    public bool OnlyWhileCodexRunning { get; set; } = true;
    public bool FloatingWidget { get; set; } = true;
    public bool TaskbarStatus { get; set; } = true;
    public bool TrayPercentage { get; set; } // Legacy preference, no longer used.
    public string TrayQuota { get; set; } = "5-hour";
    public bool EdgeBar { get; set; }
    public string Edge { get; set; } = "Top";
    public double EdgeOffset { get; set; } = 0.5;
    public string EdgeMonitor { get; set; } = "";
    public bool AlertsEnabled { get; set; } = true;
    public int WarningPercent { get; set; } = 25;
    public int CriticalPercent { get; set; } = 10;
    public string Theme { get; set; } = "System";
    public string CodexExecutable { get; set; } = "";
    public double? Left { get; set; }
    public double? Top { get; set; }
    public TrackerSettings Copy() => (TrackerSettings)MemberwiseClone();

    public static TrackerSettings Load(string path)
    {
        try
        {
            var settings = JsonSerializer.Deserialize<TrackerSettings>(File.ReadAllText(path)) ?? new();
            settings.WarningPercent = Math.Clamp(settings.WarningPercent, 1, 99);
            settings.CriticalPercent = Math.Clamp(settings.CriticalPercent, 1, settings.WarningPercent);
            if (settings.Theme is not ("System" or "Light" or "Dark")) settings.Theme = "System";
            if (settings.Edge is not ("Top" or "Left" or "Right")) settings.Edge = "Top";
            if (settings.TrayQuota is not ("5-hour" or "Weekly")) settings.TrayQuota = "5-hour";
            settings.EdgeOffset = double.IsFinite(settings.EdgeOffset) ? Math.Clamp(settings.EdgeOffset, 0, 1) : 0.5;
            return settings;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, overwrite: true);
    }
}
