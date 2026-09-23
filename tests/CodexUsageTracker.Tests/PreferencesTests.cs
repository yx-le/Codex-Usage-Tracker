using System.Text.Json;
using CodexUsageTracker.Core;
using Xunit;

namespace CodexUsageTracker.Tests;

public sealed class PreferencesTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "CodexUsageTracker.Tests", Guid.NewGuid().ToString("N"));
    private string SettingsPath => Path.Combine(directory, "settings.json");
    private TrackerSettings Saved()
    {
        var settings = new TrackerSettings { OnlyWhileCodexRunning = false, FloatingWidget = true, TaskbarStatus = false, EdgeBar = true, Edge = "Right", EdgeOffset = 0.72, EdgeMonitor = @"\\.\DISPLAY1", AlertsEnabled = false, WarningPercent = 40, CriticalPercent = 15, Theme = "Light", Left = -124, Top = 611.2 };
        settings.Save(SettingsPath); return settings;
    }
    [Fact] public void EveryPreferenceSurvivesFreshLoad()
    {
        var expected = Saved();
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(TrackerSettings.Load(SettingsPath)));
    }
    [Fact] public void CorruptPrimaryRecoversLatestSavedPreferences()
    {
        var expected = Saved(); File.WriteAllText(SettingsPath, "partial json");
        var loaded = TrackerSettings.Load(SettingsPath, out var notice);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(loaded)); Assert.Contains("backup", notice);
    }
    [Fact] public void LockedPrimaryDoesNotResetPreferences()
    {
        var expected = Saved();
        using var locked = new FileStream(SettingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(TrackerSettings.Load(SettingsPath)));
    }
    [Fact] public void UnreadablePreferencesAreNotSilentlyOverwritten()
    {
        Directory.CreateDirectory(directory); File.WriteAllText(SettingsPath, "broken");
        TrackerSettings.Load(SettingsPath, out var notice);
        Assert.NotEmpty(notice); Assert.Equal("broken", File.ReadAllText(SettingsPath));
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
