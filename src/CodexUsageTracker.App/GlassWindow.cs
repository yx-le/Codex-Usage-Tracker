namespace CodexUsageTracker.App;

internal static class GlassWindow
{
    public static void Enable(Window window, Func<bool> isDark)
    {
        window.Loaded += (_, _) => Apply(window, isDark());
        window.SizeChanged += (_, _) => Apply(window, isDark());
    }

    public static void Apply(Window window, bool dark)
    {
        // One shape controls both the tinted surface and all its child content.
        // Native acrylic paints a separate, more opaque backdrop with its own corner radius.
        // A layered WPF window instead exposes the real desktop through the brush alpha.
        if (window.Content is not Border surface) return;
        surface.Clip = new RectangleGeometry(
            new Rect(0, 0, surface.ActualWidth, surface.ActualHeight),
            surface.CornerRadius.TopLeft, surface.CornerRadius.TopLeft);
    }
}
