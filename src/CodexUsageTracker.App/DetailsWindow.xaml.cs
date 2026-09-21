using System.ComponentModel;
using System.Windows.Input;

namespace CodexUsageTracker.App;

public partial class DetailsWindow : Window
{
    private readonly TrackerController controller;
    public DetailsWindow(TrackerController controller)
    {
        InitializeComponent(); this.controller = controller; DataContext = controller.ViewModel;
        Closing += HideInsteadOfClose;
    }
    private void HideInsteadOfClose(object? sender, CancelEventArgs e) { e.Cancel = true; Hide(); }
    private async void Refresh(object sender, RoutedEventArgs e) => await controller.RefreshAsync();
    private void Settings(object sender, RoutedEventArgs e) => controller.ShowSettings();
    private void OnKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) Hide(); }
    public void SetHistory(IReadOnlyList<UsageSnapshot> samples) { Chart.Samples = samples; Chart.InvalidateVisual(); }
}
