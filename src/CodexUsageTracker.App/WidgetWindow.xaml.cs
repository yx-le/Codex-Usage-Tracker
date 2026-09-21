using System.Windows.Input;

namespace CodexUsageTracker.App;

public partial class WidgetWindow : Window
{
    private readonly TrackerController controller;
    private Point start;
    private bool down, dragged;
    public WidgetWindow(TrackerController controller)
    {
        InitializeComponent(); this.controller = controller; DataContext = controller.ViewModel;
        Left = controller.Settings.Left ?? SystemParameters.WorkArea.Right - Width - 24;
        Top = controller.Settings.Top ?? SystemParameters.WorkArea.Bottom - Height - 24;
        ClampPosition();
    }
    public void ClampPosition()
    {
        // Virtual desktop clamp preserves positions on secondary displays.
        Left = Math.Clamp(double.IsFinite(Left) ? Left : 0, SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenLeft + Math.Max(0, SystemParameters.VirtualScreenWidth - Width));
        Top = Math.Clamp(double.IsFinite(Top) ? Top : 0, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenTop + Math.Max(0, SystemParameters.VirtualScreenHeight - Height));
    }
    private void PointerDown(object sender, MouseButtonEventArgs e) { down = true; dragged = false; start = e.GetPosition(this); }
    private void PointerMove(object sender, MouseEventArgs e)
    {
        if (!down || e.LeftButton != MouseButtonState.Pressed || (e.GetPosition(this) - start).Length < 5) return;
        dragged = true; down = false; DragMove(); ClampPosition();
        controller.Settings.Left = Left; controller.Settings.Top = Top; controller.SaveSettings();
    }
    private void PointerUp(object sender, MouseButtonEventArgs e) { down = false; if (dragged) e.Handled = true; }
    private void OpenDetails(object sender, RoutedEventArgs e) { if (!dragged) controller.ToggleDetails(); }
}
