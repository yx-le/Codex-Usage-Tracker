using System.Windows.Input;
using Forms = System.Windows.Forms;

namespace CodexUsageTracker.App;

public partial class EdgeBarWindow : Window
{
    private readonly TrackerController controller;
    public EdgeBarWindow(TrackerController controller)
    {
        InitializeComponent(); this.controller = controller; DataContext = controller.ViewModel;
    }
    public void Place()
    {
        var settings = controller.Settings;
        var horizontal = settings.Edge == "Top";
        Width = horizontal ? 132 : 28; Height = horizontal ? 28 : 132;
        Surface.Padding = horizontal ? new Thickness(6, 4, 6, 4) : new Thickness(2, 4, 2, 4);
        Quotas.Visibility = horizontal ? Visibility.Visible : Visibility.Collapsed;
        SideQuotas.Visibility = horizontal ? Visibility.Collapsed : Visibility.Visible;
        Quotas.Orientation = Orientation.Horizontal;
        FiveBlock.Width = WeekBlock.Width = 54;
        FiveBlock.Margin = new Thickness(0, 0, 10, 0);
        var screen = Forms.Screen.AllScreens.FirstOrDefault(item => item.DeviceName == settings.EdgeMonitor) ?? Forms.Screen.PrimaryScreen!;
        var dpi = VisualTreeHelper.GetDpi(this); var area = screen.WorkingArea;
        var rect = EdgeBarPlacement.Place(new(area.Left / dpi.DpiScaleX, area.Top / dpi.DpiScaleY, area.Width / dpi.DpiScaleX, area.Height / dpi.DpiScaleY), settings.Edge, settings.EdgeOffset, Width, Height);
        Left = rect.Left; Top = rect.Top;
    }
    private void MoveOrOpen(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var before = new Point(Left, Top); DragMove();
        if (Math.Abs(Left - before.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(Top - before.Y) < SystemParameters.MinimumVerticalDragDistance)
        { controller.ToggleEdgeDetails(); return; }
        var screen = Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        var dpi = VisualTreeHelper.GetDpi(this); var work = screen.WorkingArea;
        var cursor = Forms.Cursor.Position;
        controller.Settings.Edge = EdgeBarPlacement.NearestEdge(new(work.Left, work.Top, work.Width, work.Height), cursor.X, cursor.Y);
        var horizontal = controller.Settings.Edge == "Top";
        Width = horizontal ? 132 : 28; Height = horizontal ? 28 : 132;
        var span = horizontal ? work.Width / dpi.DpiScaleX - Width : work.Height / dpi.DpiScaleY - Height;
        var position = horizontal ? Left - work.Left / dpi.DpiScaleX : Top - work.Top / dpi.DpiScaleY;
        controller.Settings.EdgeMonitor = screen.DeviceName;
        controller.Settings.EdgeOffset = span > 0 ? Math.Clamp(position / span, 0, 1) : 0;
        Place(); controller.SaveSettings(); controller.RepositionPanel();
    }
}
