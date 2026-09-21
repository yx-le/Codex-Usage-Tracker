namespace CodexUsageTracker.Core;

public readonly record struct LayoutRect(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
}

public static class PanelPlacement
{
    public static LayoutRect Beside(LayoutRect work, LayoutRect widget, double width, double height, double gap = 12)
    {
        width = Math.Min(width, work.Width); height = Math.Min(height, work.Height);
        var leftSpace = Math.Max(0, widget.Left - work.Left - gap);
        var rightSpace = Math.Max(0, work.Right - widget.Right - gap);
        var aboveSpace = Math.Max(0, widget.Top - work.Top - gap);
        var belowSpace = Math.Max(0, work.Bottom - widget.Bottom - gap);
        var top = Math.Clamp(widget.Bottom - height, work.Top, work.Bottom - height);
        if (leftSpace >= width) return new(widget.Left - gap - width, top, width, height);
        if (rightSpace >= width) return new(widget.Right + gap, top, width, height);
        var left = Math.Clamp(widget.Left, work.Left, work.Right - width);
        if (aboveSpace >= height) return new(left, widget.Top - gap - height, width, height);
        if (belowSpace >= height) return new(left, widget.Bottom + gap, width, height);
        // On compact displays prefer a shorter scrolling panel over covering the widget.
        if (Math.Max(aboveSpace, belowSpace) >= 240)
            return aboveSpace >= belowSpace ? new(left, work.Top, width, aboveSpace) : new(left, widget.Bottom + gap, width, belowSpace);
        return leftSpace >= rightSpace ? new(work.Left, top, Math.Max(1, leftSpace), height)
            : new(widget.Right + gap, top, Math.Max(1, rightSpace), height);
    }
}
