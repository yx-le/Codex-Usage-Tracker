using System.Windows.Input;

namespace CodexUsageTracker.App;

public partial class WidgetWindow : Window
{
    private readonly TrackerController controller;
    private readonly WidgetDragGesture gesture = new();
    public WidgetWindow(TrackerController controller)
    {
        InitializeComponent(); this.controller = controller; DataContext = controller.ViewModel;
        Left = controller.Settings.Left ?? SystemParameters.WorkArea.Right - Width - 24;
        Top = controller.Settings.Top ?? SystemParameters.WorkArea.Bottom - Height - 24;
        ClampPosition();
        LostMouseCapture += (_, _) => { if (gesture.IsActive) FinishGesture(openOnClick: false); };
        IsVisibleChanged += (_, _) => { if (!IsVisible && gesture.IsActive) FinishGesture(openOnClick: false); };
    }
    public void ClampPosition()
    {
        // Virtual desktop clamp preserves positions on secondary displays.
        Left = Math.Clamp(double.IsFinite(Left) ? Left : 0, SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenLeft + Math.Max(0, SystemParameters.VirtualScreenWidth - Width));
        Top = Math.Clamp(double.IsFinite(Top) ? Top : 0, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenTop + Math.Max(0, SystemParameters.VirtualScreenHeight - Height));
    }
    private void PointerDown(object sender, MouseButtonEventArgs e)
    {
        // Own the capture before Button can capture the pointer and consume movement.
        e.Handled = true;
        if (!CaptureMouse()) return;
        var point = PointToScreen(e.GetPosition(this));
        var dpi = VisualTreeHelper.GetDpi(this);
        gesture.Begin(point.X, point.Y, Left, Top, dpi.DpiScaleX, dpi.DpiScaleY,
            SystemParameters.MinimumHorizontalDragDistance, SystemParameters.MinimumVerticalDragDistance);
    }
    private void PointerMove(object sender, MouseEventArgs e)
    {
        if (!gesture.IsActive) return;
        e.Handled = true;
        if (e.LeftButton != MouseButtonState.Pressed) { FinishGesture(openOnClick: false); return; }
        var point = PointToScreen(e.GetPosition(this));
        if (gesture.Move(point.X, point.Y) is not { } position) return;
        Left = position.Left; Top = position.Top;
    }
    private void PointerUp(object sender, MouseButtonEventArgs e)
    {
        if (!gesture.IsActive) return;
        e.Handled = true;
        FinishGesture(openOnClick: true);
    }
    private void FinishGesture(bool openOnClick)
    {
        var moved = gesture.End(); // Clear state before releasing capture (which raises an event).
        ReleaseMouseCapture();
        if (moved)
        {
            ClampPosition();
            controller.Settings.Left = Left; controller.Settings.Top = Top; controller.SaveSettings();
        }
        else if (openOnClick) controller.ToggleDetails();
    }
    // Keyboard and accessibility activation still use the standard button action.
    private void OpenDetails(object sender, RoutedEventArgs e) => controller.ToggleDetails();
}
