using System.Globalization;
using System.Windows.Media.Imaging;

namespace CodexUsageTracker.App;

// Reproducible README illustration: real WPF UI over a synthetic desktop, never
// a screenshot of the user's private windows or wallpaper.
internal static class PreviewScene
{
    public static void Save(string path, Window window, bool details)
    {
        window.UpdateLayout();
        var ui = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        ui.Render(window);
        var scene = new DrawingVisual();
        using (var dc = scene.RenderOpen())
        {
            void Text(string value, double x, double y, double size, string color) => dc.DrawText(new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, (Brush)new BrushConverter().ConvertFromString(color)!, 1), new Point(x, y));
            var background = new LinearGradientBrush(Color.FromRgb(28, 43, 56), Color.FromRgb(71, 89, 103), 35);
            dc.DrawRectangle(background, null, new Rect(0, 0, 1280, 900));
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(25, 164, 211, 216)), null, new Point(240, 760), 690, 450);
            Text("Codex Usage Tracker", 48, 30, 24, "#F1F5F9");
            Text(details ? "Details when you need them" : "A quiet glance at your remaining quota", 48, 66, 15, "#CAD5DE");
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(23, 30, 39)), null, new Rect(48, 133, 890, 627), 14, 14);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(34, 43, 54)), null, new Rect(48, 133, 890, 45), 14, 14);
            Text("Workspace   /   Notes", 72, 147, 13, "#BECBD6");
            Text("Today's focus", 92, 215, 26, "#E6EDF3");
            Text("Small improvements. More room to work.", 92, 256, 16, "#9BACBB");
            foreach (var (title, description, y) in new[] { ("01   Plan", "Keep the essentials in view.", 331d), ("02   Build", "Focus on one change at a time.", 427d), ("03   Review", "Check the result, then move forward.", 523d) })
            {
                Text(title, 92, y, 17, "#CDD8E1"); Text(description, 92, y + 29, 13, "#8295A6");
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(47, 61, 74)), 1), new Point(92, y + 65), new Point(705, y + 65));
            }
            var target = details ? new Rect(822, 58, ui.Width, ui.Height) : new Rect(1120, 673, ui.Width, ui.Height);
            dc.DrawImage(ui, target);
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(25, 33, 43)), null, new Rect(0, 852, 1280, 48));
            Text("Illustrative desktop · actual app rendering · synthetic quota", 48, 867, 12, "#A7B6C3");
        }
        var bitmap = new RenderTargetBitmap(1280, 900, 96, 96, PixelFormats.Pbgra32); bitmap.Render(scene);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}
