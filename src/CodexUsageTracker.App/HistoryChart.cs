using System.Globalization;
using System.Windows.Input;
using System.Windows.Controls.Primitives;

namespace CodexUsageTracker.App;

public sealed class HistoryChart : FrameworkElement
{
    public IReadOnlyList<UsageSnapshot> Samples { get; set; } = Array.Empty<UsageSnapshot>();
    public int Days { get; set; } = 1;
    private readonly List<(Point Position, HistoryPoint Reading, string Name, Brush Color)> hits = new();
    private readonly ToolTip popup = new() { Placement = PlacementMode.Mouse, StaysOpen = true, Padding = new Thickness(10, 8, 10, 8) };
    private Point? focus;

    public HistoryChart()
    {
        SnapsToDevicePixels = true;
        MouseLeave += (_, _) => { popup.IsOpen = false; focus = null; InvalidateVisual(); };
        Unloaded += (_, _) => popup.IsOpen = false;
    }
    protected override void OnRender(DrawingContext drawing)
    {
        const double left = 29, right = 8, laneHeight = 101, plotHeight = 65;
        var width = Math.Max(1, ActualWidth - left - right);
        var muted = (Brush)FindResource("MutedBrush");
        var text = (Brush)FindResource("TextBrush");
        var now = DateTimeOffset.UtcNow;
        var from = now.AddDays(-Days);
        double X(DateTimeOffset time) => left + (time - from).TotalDays / Days * width;
        hits.Clear();
        drawing.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        void Lane(int index, string name, Func<UsageSnapshot, QuotaWindow?> select, Brush color)
        {
            var top = 25 + index * laneHeight;
            double Y(double remaining) => top + (100 - remaining) / 100 * plotHeight;
            Label(drawing, name, color, new Point(0, top - 23), 12, true);
            foreach (var value in new[] { 0, 50, 100 })
            {
                drawing.DrawLine(new Pen((Brush)FindResource("BorderBrush"), value == 50 ? 0.5 : 0.8), new Point(left, Y(value)), new Point(left + width, Y(value)));
                Label(drawing, value.ToString(CultureInfo.InvariantCulture), muted, new Point(0, Y(value) - 6), 10);
            }
            var segments = HistorySeries.Build(Samples, select, now, Days);
            var pen = new Pen(color, 2) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            drawing.PushClip(new RectangleGeometry(new Rect(left - 3, top - 4, width + 6, plotHeight + 8)));
            foreach (var segment in segments)
            {
                // Keep actual readings, one per pixel, instead of drawing dense vertical fences.
                var points = segment.GroupBy(p => (int)X(p.Time)).Select(g => g.Last()).ToArray();
                foreach (var p in points) hits.Add((new Point(X(p.Time), Y(p.Remaining)), p, name, color));
                if (points.Length == 1)
                {
                    drawing.DrawEllipse(color, null, new Point(X(points[0].Time), Y(points[0].Remaining)), 1.8, 1.8);
                    continue;
                }
                var path = new StreamGeometry();
                using (var context = path.Open())
                {
                    context.BeginFigure(new Point(X(points[0].Time), Y(points[0].Remaining)), false, false);
                    foreach (var p in points.Skip(1)) context.LineTo(new Point(X(p.Time), Y(p.Remaining)), true, false);
                }
                path.Freeze();
                drawing.DrawGeometry(null, pen, path);
            }
            var latest = segments.LastOrDefault()?.LastOrDefault();
            if (latest is not null)
                drawing.DrawEllipse(color, new Pen((Brush)FindResource("GlassStrongBrush"), 1.5), new Point(X(latest.Time), Y(latest.Remaining)), 3.4, 3.4);
            drawing.Pop();
            if (latest is null) Label(drawing, "No readings in this period", muted, new Point(left + 15, top + 25), 11);
            else Label(drawing, $"{latest.Remaining:0}%", text, new Point(ActualWidth - 43, top - 23), 12, true);
        }
        Lane(0, "5-hour", s => s.FiveHour, (Brush)FindResource("AccentBrush"));
        Lane(1, "Weekly", s => s.Weekly, (Brush)FindResource("WeekBrush"));
        for (var tick = 0; tick <= 4; tick++)
        {
            var value = tick == 4 ? "Now" : from.AddDays(Days * tick / 4d).ToLocalTime().ToString(Days == 1 ? "HH:mm" : "d MMM");
            var label = Format(value, muted, 10);
            var x = left + width * tick / 4d - (tick == 0 ? 0 : tick == 4 ? label.Width : label.Width / 2);
            drawing.DrawText(label, new Point(x, 196));
        }
        if (focus is { } p) drawing.DrawEllipse((Brush)FindResource("TextBrush"), null, p, 3, 3);
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var cursor = e.GetPosition(this);
        var candidate = hits.Where(h => Math.Abs(h.Position.Y - cursor.Y) < 36 && Math.Abs(h.Position.X - cursor.X) < 12)
            .OrderBy(h => (h.Position - cursor).LengthSquared).FirstOrDefault();
        if (candidate.Reading is null) { popup.IsOpen = false; focus = null; InvalidateVisual(); return; }
        focus = candidate.Position;
        popup.Background = (Brush)FindResource("GlassStrongBrush");
        popup.Foreground = (Brush)FindResource("TextBrush");
        popup.BorderBrush = candidate.Color;
        popup.Content = $"{candidate.Name} · {candidate.Reading.Remaining:0.#}% remaining\n{candidate.Reading.Time.ToLocalTime():ddd, d MMM · HH:mm}";
        popup.PlacementTarget = this;
        popup.IsOpen = true;
        InvalidateVisual();
    }
    private FormattedText Format(string value, Brush brush, double size, bool bold = false) => new(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
        new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    private void Label(DrawingContext dc, string value, Brush brush, Point point, double size, bool bold = false) => dc.DrawText(Format(value, brush, size, bold), point);
}
