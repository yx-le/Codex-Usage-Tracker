namespace CodexUsageTracker.Core;

public static class WidgetPlacement
{
    // All rectangles use physical screen pixels, including negative monitor origins.
    public static LayoutRect InsideNearestWorkArea(LayoutRect widget, IReadOnlyList<LayoutRect> workAreas)
    {
        if (workAreas.Count == 0) return widget;
        return workAreas.Select(area => new LayoutRect(
                Math.Clamp(widget.Left, area.Left, Math.Max(area.Left, area.Right - widget.Width)),
                Math.Clamp(widget.Top, area.Top, Math.Max(area.Top, area.Bottom - widget.Height)),
                widget.Width, widget.Height))
            .OrderBy(candidate => Math.Pow(candidate.Left - widget.Left, 2) + Math.Pow(candidate.Top - widget.Top, 2))
            .First();
    }
}
