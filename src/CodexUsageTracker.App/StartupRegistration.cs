using Microsoft.Win32;

namespace CodexUsageTracker.App;

internal static class StartupRegistration
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "CodexUsageTracker";
    public static bool IsEnabled
    {
        get
        {
            try { using var key = Registry.CurrentUser.OpenSubKey(KeyPath); return key?.GetValue(Name) is string; }
            catch (System.Security.SecurityException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        if (!enabled) { key.DeleteValue(Name, false); return; }
        var executable = Environment.ProcessPath;
        if (!string.Equals(Path.GetFileName(executable), "CodexUsageTracker.exe", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Use the installed executable to enable startup.");
        key.SetValue(Name, $"\"{executable}\" --watch-codex", RegistryValueKind.String);
        EnsureWatcher();
    }
    public static void EnsureWatcher()
    {
        if (Mutex.TryOpenExisting(@"Local\CodexUsageTracker.Watcher", out var active)) { active.Dispose(); return; }
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!, "--watch-codex") { UseShellExecute = false, CreateNoWindow = true });
    }
}
