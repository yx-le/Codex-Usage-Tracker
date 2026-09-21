using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace CodexUsageTracker.App;

internal sealed class TrayMenuRenderer : Forms.ToolStripProfessionalRenderer
{
    internal static Drawing.Color ColorFor(string resource)
    {
        var color = ((SolidColorBrush)Application.Current.FindResource(resource)).Color;
        return Drawing.Color.FromArgb(color.R, color.G, color.B);
    }

    protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e)
    {
        using var brush = new Drawing.SolidBrush(ColorFor("GlassStrongBrush"));
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }
    protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        using var brush = new Drawing.SolidBrush(ColorFor("ButtonHoverBrush"));
        e.Graphics.FillRectangle(brush, new Drawing.Rectangle(3, 1, e.Item.Width - 6, e.Item.Height - 2));
    }
    protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = ColorFor(e.Item.Enabled ? "TextBrush" : "MutedBrush");
        Forms.TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, e.TextRectangle, e.TextColor, e.TextFormat);
    }
    protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Drawing.Pen(ColorFor("BorderBrush"));
        e.Graphics.DrawLine(pen, 12, e.Item.Height / 2, e.Item.Width - 12, e.Item.Height / 2);
    }
    protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e) { }
}
