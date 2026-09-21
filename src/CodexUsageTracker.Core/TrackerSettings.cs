using System.Text.Json;

namespace CodexUsageTracker.Core;

public sealed class TrackerSettings
{
    public bool OnlyWhileCodexRunning { get; set; } = true;
    public bool FloatingWidget { get; set; } = true;
    public bool AlertsEnabled { get; set; } = true;
    public int WarningPercent { get; set; } = 25;
    public int CriticalPercent { get; set; } = 10;
    public string Theme { get; set; } = "System";
    public string CodexExecutable { get; set; } = "";
    public double? Left { get; set; }
    public double? Top { get; set; }

    public static TrackerSettings Load(string path)
    {
        try
        {
            var settings = JsonSerializer.Deserialize<TrackerSettings>(File.ReadAllText(path)) ?? new();
            settings.WarningPercent = Math.Clamp(settings.WarningPercent, 1, 99);
            settings.CriticalPercent = Math.Clamp(settings.CriticalPercent, 1, settings.WarningPercent);
            if (settings.Theme is not ("System" or "Light" or "Dark")) settings.Theme = "System";
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
