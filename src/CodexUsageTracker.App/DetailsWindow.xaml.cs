using System.ComponentModel;
using System.Windows.Input;

namespace CodexUsageTracker.App;

public partial class DetailsWindow : Window
{
    private readonly TrackerController controller;
    public DetailsWindow(TrackerController controller)
    {
        InitializeComponent(); this.controller = controller; DataContext = controller.ViewModel;
        GlassWindow.Enable(this, () => App.IsDarkTheme(controller.Settings.Theme));
        Closing += HideInsteadOfClose;
    }
    private void HideInsteadOfClose(object? sender, CancelEventArgs e) { e.Cancel = true; Hide(); }
    private async void Refresh(object sender, RoutedEventArgs e) => await controller.RefreshAsync();
    private void Settings(object sender, RoutedEventArgs e) => controller.ShowSettings();
    private void Collapse(object sender, RoutedEventArgs e) => Hide();
    private void DragWindow(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        if (SettingsHost.Visibility == Visibility.Visible) ShowUsagePage(); else Hide();
    }
    public bool IsSettingsPage => SettingsHost.Visibility == Visibility.Visible;
    public void ShowSettingsPage()
    {
        if (IsSettingsPage) return;
        UsagePage.Visibility = Visibility.Collapsed;
        SettingsHost.Content = new SettingsPage(controller);
        SettingsHost.Visibility = Visibility.Visible;
    }
    public void ShowUsagePage()
    {
        SettingsHost.Visibility = Visibility.Collapsed; SettingsHost.Content = null;
        UsagePage.Visibility = Visibility.Visible;
    }
    public void SetHistory(IReadOnlyList<UsageSnapshot> samples) { Chart.Samples = samples; Chart.InvalidateVisual(); }
}
