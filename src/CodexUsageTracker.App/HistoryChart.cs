using System.Globalization;

namespace CodexUsageTracker.App;

public sealed class HistoryChart : FrameworkElement
{
    public IReadOnlyList<UsageSnapshot> Samples { get; set; } = Array.Empty<UsageSnapshot>();
    protected override void OnRender(DrawingContext drawing)
    {
        var width = Math.Max(1, ActualWidth - 28); var height = Math.Max(1, ActualHeight - 18);
        var muted = (Brush)FindResource("MutedBrush");
        foreach (var percent in new[] { 0, 50, 100 })
        {
            var y = (100 - percent) / 100d * height;
            drawing.DrawLine(new Pen((Brush)FindResource("BorderBrush"), 0.6), new Point(26, y), new Point(ActualWidth, y));
            Label(drawing, percent.ToString(CultureInfo.InvariantCulture), muted, new Point(0, Math.Max(0, y - 6)));
        }
        var now = DateTimeOffset.UtcNow; var from = now.AddDays(-7);
        void Series(Func<UsageSnapshot, QuotaWindow?> select, Brush color)
        {
            Point? previous = null; DateTimeOffset? previousTime = null; DateTimeOffset? previousReset = null;
            foreach (var sample in Samples)
            {
                var window = select(sample);
                if (window is null) { previous = null; continue; }
                var point = new Point(26 + Math.Clamp((sample.ObservedAt - from).TotalDays / 7, 0, 1) * width, (100 - window.RemainingPercent) / 100 * height);
                if (previous is { } last && sample.ObservedAt - previousTime < TimeSpan.FromMinutes(10) && previousReset == window.ResetsAt)
                    drawing.DrawLine(new Pen(color, 1.8), last, point);
                drawing.DrawEllipse(color, null, point, 1.8, 1.8);
                previous = point; previousTime = sample.ObservedAt; previousReset = window.ResetsAt;
            }
        }
        Series(sample => sample.FiveHour, (Brush)FindResource("AccentBrush"));
        Series(sample => sample.Weekly, (Brush)FindResource("WeekBrush"));
        if (Samples.Count == 0) Label(drawing, "History appears as readings arrive", muted, new Point(40, height / 2 - 6));
        Label(drawing, from.ToLocalTime().ToString("ddd"), muted, new Point(26, height + 3));
        Label(drawing, "Now", muted, new Point(ActualWidth - 24, height + 3));
    }
    private void Label(DrawingContext dc, string value, Brush brush, Point point) => dc.DrawText(new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip), point);
}
