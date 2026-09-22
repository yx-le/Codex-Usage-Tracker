using Microsoft.Win32;
using System.Windows.Input;

namespace CodexUsageTracker.App;

public partial class SettingsPage : UserControl
{
    private readonly TrackerController controller;
    public SettingsPage(TrackerController controller)
    {
        InitializeComponent(); this.controller = controller;
        var settings = controller.Settings;
        Startup.IsChecked = StartupRegistration.IsEnabled;
        OnlyRunning.IsChecked = settings.OnlyWhileCodexRunning; Floating.IsChecked = settings.FloatingWidget;
        Alerts.IsChecked = settings.AlertsEnabled; Warning.Text = settings.WarningPercent.ToString(); Critical.Text = settings.CriticalPercent.ToString();
        ThemeChoice.SelectedIndex = settings.Theme == "Light" ? 1 : settings.Theme == "Dark" ? 2 : 0;
        Executable.Text = settings.CodexExecutable;
        TaskbarEnabled.IsChecked = settings.TaskbarStatus;
        EdgeEnabled.IsChecked = settings.EdgeBar;
    }
    private void Cancel(object sender, RoutedEventArgs e) => controller.ShowUsagePage();
    private void DragWindow(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) Window.GetWindow(this)?.DragMove(); }
    private void Browse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Codex executable (codex.exe)|codex.exe", CheckFileExists = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) Executable.Text = dialog.FileName;
    }
    private void Save(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(Warning.Text, out var warning) || !int.TryParse(Critical.Text, out var critical) || critical < 1 || warning > 99 || critical >= warning)
        { Validation.Text = "Use percentages from 1 to 99; critical must be below warning."; return; }
        var executable = Executable.Text.Trim();
        if (executable.Length > 0 && (!File.Exists(executable) || !string.Equals(Path.GetFileName(executable), "codex.exe", StringComparison.OrdinalIgnoreCase)))
        { Validation.Text = "Choose an existing codex.exe, or leave the path blank."; return; }
        var settings = controller.Settings.Copy();
        settings.TaskbarStatus = TaskbarEnabled.IsChecked == true; settings.TrayPercentage = false;
        settings.EdgeBar = EdgeEnabled.IsChecked == true;
        settings.OnlyWhileCodexRunning = OnlyRunning.IsChecked == true; settings.FloatingWidget = Floating.IsChecked == true;
        settings.AlertsEnabled = Alerts.IsChecked == true; settings.WarningPercent = warning; settings.CriticalPercent = critical;
        settings.Theme = ((ComboBoxItem)ThemeChoice.SelectedItem).Content.ToString()!; settings.CodexExecutable = executable;
        if (!controller.TrySaveSettings(settings, out var error)) { Validation.Text = error; return; }
        controller.ShowUsagePage();
    }
    private void ChangeStartup(object sender, RoutedEventArgs e)
    {
        try { StartupRegistration.SetEnabled(Startup.IsChecked == true); Validation.Text = "Startup preference updated."; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { Startup.IsChecked = StartupRegistration.IsEnabled; Validation.Text = "Could not change startup. Check your Windows permissions and try again."; }
    }
    private async void ClearHistory(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender; button.IsEnabled = false;
        try { await controller.ClearHistoryAsync(); Validation.Text = "Local history cleared. New readings will start a fresh history."; }
        catch (Exception error) when (error is Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException)
        { Validation.Text = "History could not be cleared. Check folder access or try again shortly."; }
        finally { button.IsEnabled = true; }
    }
}
