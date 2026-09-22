namespace CodexUsageTracker.Core;

public static class EdgeBarPlacement
{
    public static LayoutRect Place(LayoutRect work, string edge, double offset, double width, double height)
    {
        offset = double.IsFinite(offset) ? Math.Clamp(offset, 0, 1) : 0.5;
        width = Math.Min(width, work.Width); height = Math.Min(height, work.Height);
        return edge switch
        {
            "Left" => new(work.Left, work.Top + (work.Height - height) * offset, width, height),
            "Right" => new(work.Right - width, work.Top + (work.Height - height) * offset, width, height),
            _ => new(work.Left + (work.Width - width) * offset, work.Top, width, height)
        };
    }
}
