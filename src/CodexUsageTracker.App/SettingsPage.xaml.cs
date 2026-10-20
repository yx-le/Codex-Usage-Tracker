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
        CircleTransparency.Value = settings.CircleTransparency;
        EdgeTransparency.Value = settings.EdgeTransparency;
        PanelTransparency.Value = settings.PanelTransparency;
        UpdateTransparencyLabels();
        // Subscribe after initialization: populating controls must never overwrite
        // the preferences loaded from disk with XAML defaults.
        foreach (var checkbox in new[] { OnlyRunning, Floating, TaskbarEnabled, EdgeEnabled, Alerts })
            checkbox.Click += (_, _) => SaveDisplayPreferences();
        ThemeChoice.SelectionChanged += (_, _) => SaveDisplayPreferences();
        foreach (var slider in new[] { CircleTransparency, EdgeTransparency, PanelTransparency })
            slider.ValueChanged += (_, _) => { UpdateTransparencyLabels(); SaveDisplayPreferences(); };
        Warning.TextChanged += (_, _) => SaveAdvancedPreferences();
        Critical.TextChanged += (_, _) => SaveAdvancedPreferences();
        Executable.TextChanged += (_, _) => SaveAdvancedPreferences();
        if (controller.PreferencesNotice.Length > 0) Validation.Text = controller.PreferencesNotice;
        Sections.SelectionChanged += (_, e) =>
        {
            if (e.Source != Sections) return;
            SettingsScroll.ScrollToTop();
            Dispatcher.BeginInvoke(() => controller.RepositionPanel());
        };
    }
    private void Cancel(object sender, RoutedEventArgs e) => controller.ShowUsagePage();
    private void UpdateTransparencyLabels()
    {
        CircleTransparencyValue.Text = $"{CircleTransparency.Value:0}%";
        EdgeTransparencyValue.Text = $"{EdgeTransparency.Value:0}%";
        PanelTransparencyValue.Text = $"{PanelTransparency.Value:0}%";
    }
    private void DragWindow(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) Window.GetWindow(this)?.DragMove(); }
    private void Browse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Codex executable (codex.exe)|codex.exe", CheckFileExists = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) Executable.Text = dialog.FileName;
    }
    private void Save(object sender, RoutedEventArgs e)
    {
        if (SaveDisplayPreferences() && SaveAdvancedPreferences()) controller.ShowUsagePage();
    }
    private bool SaveDisplayPreferences()
    {
        var settings = controller.Settings.Copy();
        settings.TaskbarStatus = TaskbarEnabled.IsChecked == true;
        settings.EdgeBar = EdgeEnabled.IsChecked == true;
        settings.OnlyWhileCodexRunning = OnlyRunning.IsChecked == true;
        settings.FloatingWidget = Floating.IsChecked == true;
        settings.AlertsEnabled = Alerts.IsChecked == true;
        settings.Theme = ((ComboBoxItem)ThemeChoice.SelectedItem).Content.ToString()!;
        settings.CircleTransparency = (int)CircleTransparency.Value;
        settings.EdgeTransparency = (int)EdgeTransparency.Value;
        settings.PanelTransparency = (int)PanelTransparency.Value;
        return Persist(settings);
    }
    private bool SaveAdvancedPreferences()
    {
        if (!int.TryParse(Warning.Text, out var warning) || !int.TryParse(Critical.Text, out var critical) || critical < 1 || warning > 99 || critical >= warning)
        { Validation.Text = "Not saved: use percentages from 1 to 99; critical must be below warning."; return false; }
        var executable = Executable.Text.Trim();
        if (executable.Length > 0 && (!File.Exists(executable) || !string.Equals(Path.GetFileName(executable), "codex.exe", StringComparison.OrdinalIgnoreCase)))
        { Validation.Text = "Not saved: choose an existing codex.exe, or leave the path blank."; return false; }
        var settings = controller.Settings.Copy();
        settings.WarningPercent = warning; settings.CriticalPercent = critical; settings.CodexExecutable = executable;
        return Persist(settings);
    }
    private bool Persist(TrackerSettings settings)
    {
        if (!controller.TrySaveSettings(settings, out var error)) { Validation.Text = error; return false; }
        Validation.Text = "Saved automatically";
        return true;
    }
    private void ChangeStartup(object sender, RoutedEventArgs e)
    {
        try { StartupRegistration.SetEnabled(Startup.IsChecked == true); Validation.Text = "Startup preference updated."; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { Startup.IsChecked = StartupRegistration.IsEnabled; Validation.Text = "Could not change startup. Check your Windows permissions and try again."; }
    }
}
