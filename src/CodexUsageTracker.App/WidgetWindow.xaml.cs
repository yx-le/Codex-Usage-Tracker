using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Windows.Interop;

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
        SourceInitialized += (_, _) => ClampPosition();
        LostMouseCapture += (_, _) => { if (gesture.IsActive) FinishGesture(openOnClick: false); };
        IsVisibleChanged += (_, _) => { if (!IsVisible && gesture.IsActive) FinishGesture(openOnClick: false); };
    }
    public void ClampPosition()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out var rect)) return;
        // Work in native pixels so monitor origins and mixed display scales agree.
        var areas = System.Windows.Forms.Screen.AllScreens.Select(screen => screen.WorkingArea)
            .Select(area => new LayoutRect(area.Left, area.Top, area.Width, area.Height)).ToArray();
        var placement = WidgetPlacement.InsideNearestWorkArea(new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top), areas);
        SetWindowPos(handle, IntPtr.Zero, (int)placement.Left, (int)placement.Top, 0, 0, 0x0001 | 0x0004 | 0x0010);
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
        controller.RepositionPanel();
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
            controller.RepositionPanel();
            controller.Settings.Left = Left; controller.Settings.Top = Top; controller.SaveSettings();
        }
        else if (openOnClick) controller.ToggleDetails();
    }
    // Keyboard and accessibility activation still use the standard button action.
    private void OpenDetails(object sender, RoutedEventArgs e) => controller.ToggleDetails();
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr handle, out NativeRect rectangle);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
}
