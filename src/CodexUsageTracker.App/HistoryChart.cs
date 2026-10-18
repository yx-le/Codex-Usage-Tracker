using System.Globalization;

namespace CodexUsageTracker.App;

public sealed class HistoryChart : FrameworkElement
{
    public IReadOnlyList<UsageSnapshot> Samples { get; set; } = Array.Empty<UsageSnapshot>();
    protected override void OnRender(DrawingContext drawing)
    {
        const double left = 28, top = 5, right = 8, bottom = 20;
        var width = Math.Max(1, ActualWidth - left - right);
        var height = Math.Max(1, ActualHeight - top - bottom);
        var muted = (Brush)FindResource("MutedBrush");
        var now = DateTimeOffset.UtcNow; var from = now.AddDays(-7);
        double X(DateTimeOffset time) => left + (time - from).TotalDays / 7 * width;
        double Y(double percent) => top + (100 - percent) / 100d * height;
        foreach (var percent in new[] { 0, 50, 100 })
        {
            drawing.DrawLine(new Pen((Brush)FindResource("BorderBrush"), 0.6), new Point(left, Y(percent)), new Point(left + width, Y(percent)));
            Label(drawing, percent.ToString(CultureInfo.InvariantCulture), muted, new Point(0, Y(percent) - 5));
        }
        for (var day = 0; day <= 4; day += 2)
        {
            var time = from.AddDays(day);
            Label(drawing, time.ToLocalTime().ToString("d MMM"), muted, new Point(X(time), top + height + 4));
        }
        Label(drawing, "Now", muted, new Point(ActualWidth - 25, top + height + 4));
        void Series(Func<UsageSnapshot, QuotaWindow?> select, Brush color)
        {
            var segments = HistorySeries.Build(Samples, select, now);
            var pen = new Pen(color, 1.8) { LineJoin = PenLineJoin.Round };
            var gapPen = new Pen(color, 1) { DashStyle = DashStyles.Dash };
            drawing.PushClip(new RectangleGeometry(new Rect(left - 3, top - 3, width + 6, height + 6)));
            foreach (var segment in segments)
            {
                // At most four vertices per screen column, preserving local extrema.
                var points = segment.GroupBy(p => (int)X(p.Time)).SelectMany(g =>
                    new[] { g.First(), g.MinBy(p => p.Remaining)!, g.MaxBy(p => p.Remaining)!, g.Last() }
                        .Distinct().OrderBy(p => p.Time)).ToArray();
                for (var i = 1; i < points.Length; i++)
                    drawing.DrawLine(points[i].Time - points[i - 1].Time > TimeSpan.FromMinutes(10) ? gapPen : pen,
                        new Point(X(points[i - 1].Time), Y(points[i - 1].Remaining)), new Point(X(points[i].Time), Y(points[i].Remaining)));
            }
            if (segments.LastOrDefault()?.LastOrDefault() is { } latest)
                drawing.DrawEllipse(color, null, new Point(X(latest.Time), Y(latest.Remaining)), 2.5, 2.5);
            drawing.Pop();
        }
        Series(s => s.FiveHour, (Brush)FindResource("AccentBrush"));
        Series(s => s.Weekly, (Brush)FindResource("WeekBrush"));
        if (Samples.Count == 0) Label(drawing, "History appears as readings arrive", muted, new Point(40, top + height / 2 - 6));
    }
    private void Label(DrawingContext dc, string value, Brush brush, Point point) => dc.DrawText(new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip), point);
}
