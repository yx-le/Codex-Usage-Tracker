using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace CodexUsageTracker.App;

internal sealed class TrayMenuRenderer : Forms.ToolStripProfessionalRenderer
{
    private static bool Dark => ColorFor("TextBrush").R > 128;
    internal static Drawing.Color Background => Dark ? Drawing.Color.FromArgb(32, 32, 32) : Drawing.Color.FromArgb(250, 250, 250);
    internal static Drawing.Color Foreground => Dark ? Drawing.Color.FromArgb(242, 242, 242) : Drawing.Color.FromArgb(30, 30, 30);
    internal static Drawing.Drawing2D.GraphicsPath Rounded(Drawing.Rectangle bounds, int radius)
    {
        var path = new Drawing.Drawing2D.GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure(); return path;
    }
    internal static Drawing.Color ColorFor(string resource)
    {
        var color = ((SolidColorBrush)Application.Current.FindResource(resource)).Color;
        return Drawing.Color.FromArgb(color.R, color.G, color.B);
    }

    protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e)
    {
        e.Graphics.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var brush = new Drawing.SolidBrush(Background);
        using var shape = Rounded(e.ToolStrip.ClientRectangle, 10 * e.ToolStrip.DeviceDpi / 96);
        e.Graphics.FillPath(brush, shape);
    }
    protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        using var brush = new Drawing.SolidBrush(Dark ? Drawing.Color.FromArgb(53, 53, 53) : Drawing.Color.FromArgb(233, 233, 233));
        using var shape = Rounded(new Drawing.Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2), 5);
        e.Graphics.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.FillPath(brush, shape);
    }
    protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Foreground : Dark ? Drawing.Color.FromArgb(163, 163, 163) : Drawing.Color.FromArgb(103, 103, 103);
        var bounds = e.TextRectangle;
        var format = e.TextFormat;
        bounds.Offset(8, 0);
        Forms.TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, bounds, e.TextColor, format);
        if (e.Item.Tag is string value)
            Forms.TextRenderer.DrawText(e.Graphics, value, e.TextFont,
                new Drawing.Rectangle(10, 0, e.Item.Width - 28, e.Item.Height), e.TextColor,
                Forms.TextFormatFlags.Right | Forms.TextFormatFlags.VerticalCenter | Forms.TextFormatFlags.SingleLine);
    }
    protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Drawing.Pen(Dark ? Drawing.Color.FromArgb(64, 64, 64) : Drawing.Color.FromArgb(222, 222, 222));
        e.Graphics.DrawLine(pen, 2, e.Item.Height / 2, e.Item.Width - 2, e.Item.Height / 2);
    }
    protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e) { }
}

internal sealed class RoundedTrayMenu : Forms.ContextMenuStrip
{
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (Width < 20 || Height < 20) return;
        using var shape = TrayMenuRenderer.Rounded(ClientRectangle, 10 * DeviceDpi / 96);
        var old = Region;
        Region = new Drawing.Region(shape);
        old?.Dispose();
    }
}
