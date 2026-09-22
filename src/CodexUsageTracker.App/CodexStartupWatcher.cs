using System.Diagnostics;
using System.Windows.Threading;

namespace CodexUsageTracker.App;

internal sealed class CodexStartupWatcher : IDisposable
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool wasRunning;
    public CodexStartupWatcher() { timer.Tick += Tick; timer.Start(); Tick(null, EventArgs.Empty); }
    private void Tick(object? sender, EventArgs e)
    {
        if (!StartupRegistration.IsEnabled) { Application.Current.Shutdown(); return; }
        var running = CodexLocator.IsRunning();
        if (running && !wasRunning)
        {
            if (Mutex.TryOpenExisting(@"Local\CodexUsageTracker", out var active)) active.Dispose();
            else
            {
                try { Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true }); }
                catch (Exception error) when (error is IOException or System.ComponentModel.Win32Exception) { return; }
            }
        }
        wasRunning = running;
    }
    public void Dispose() => timer.Stop();
}
