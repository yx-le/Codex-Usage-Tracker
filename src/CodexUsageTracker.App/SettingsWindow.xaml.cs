using Microsoft.Win32;
using System.Windows.Input;

namespace CodexUsageTracker.App;

public partial class SettingsWindow : Window
{
    private readonly TrackerController controller;
    public SettingsWindow(TrackerController controller)
    {
        InitializeComponent(); this.controller = controller;
        GlassWindow.Enable(this, () => App.IsDarkTheme(controller.Settings.Theme));
        var settings = controller.Settings;
        OnlyRunning.IsChecked = settings.OnlyWhileCodexRunning; Floating.IsChecked = settings.FloatingWidget;
        Alerts.IsChecked = settings.AlertsEnabled; Warning.Text = settings.WarningPercent.ToString(); Critical.Text = settings.CriticalPercent.ToString();
        ThemeChoice.SelectedIndex = settings.Theme == "Light" ? 1 : settings.Theme == "Dark" ? 2 : 0;
        Executable.Text = settings.CodexExecutable;
    }
    private void Cancel(object sender, RoutedEventArgs e) => DialogResult = false;
    private void DragWindow(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
    private void Browse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Codex executable (codex.exe)|codex.exe", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) Executable.Text = dialog.FileName;
    }
    private void Save(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(Warning.Text, out var warning) || !int.TryParse(Critical.Text, out var critical) || critical < 1 || warning > 99 || critical >= warning)
        { Validation.Text = "Use percentages from 1 to 99; critical must be below warning."; return; }
        var executable = Executable.Text.Trim();
        if (executable.Length > 0 && (!File.Exists(executable) || !string.Equals(Path.GetFileName(executable), "codex.exe", StringComparison.OrdinalIgnoreCase)))
        { Validation.Text = "Choose an existing codex.exe, or leave the path blank."; return; }
        var settings = controller.Settings;
        settings.OnlyWhileCodexRunning = OnlyRunning.IsChecked == true; settings.FloatingWidget = Floating.IsChecked == true;
        settings.AlertsEnabled = Alerts.IsChecked == true; settings.WarningPercent = warning; settings.CriticalPercent = critical;
        settings.Theme = ((ComboBoxItem)ThemeChoice.SelectedItem).Content.ToString()!; settings.CodexExecutable = executable;
        controller.SaveSettings(); DialogResult = true;
    }
    private void ClearHistory(object sender, RoutedEventArgs e)
    {
        try { controller.ClearHistory(); Validation.Text = "Local history cleared. New readings will start a fresh history."; }
        catch (Microsoft.Data.Sqlite.SqliteException) { Validation.Text = "History is busy. Try again after the current refresh."; }
    }
}
