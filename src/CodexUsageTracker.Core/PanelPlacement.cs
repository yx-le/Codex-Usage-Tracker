namespace CodexUsageTracker.Core;

public readonly record struct LayoutRect(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
}

public static class PanelPlacement
{
    public static LayoutRect FromEdgeBar(LayoutRect work, LayoutRect bar, string edge, double width, double height, double gap = 12)
    {
        width = Math.Min(width, work.Width);
        height = Math.Min(height, work.Height);
        if (edge == "Left" || edge == "Right")
        {
            var available = edge == "Left" ? work.Right - bar.Right : bar.Left - work.Left;
            gap = Math.Min(gap, Math.Max(0, available - 1));
            width = Math.Min(width, Math.Max(0, available - gap));
            var x = edge == "Left" ? bar.Right + gap : bar.Left - gap - width;
            var y = Math.Clamp(bar.Top + (bar.Height - height) / 2, work.Top, work.Bottom - height);
            return new(x, y, width, height);
        }
        var below = work.Bottom - bar.Bottom;
        gap = Math.Min(gap, Math.Max(0, below - 1));
        height = Math.Min(height, Math.Max(0, below - gap));
        var left = Math.Clamp(bar.Left + (bar.Width - width) / 2, work.Left, work.Right - width);
        return new(left, bar.Bottom + gap, width, height);
    }

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
