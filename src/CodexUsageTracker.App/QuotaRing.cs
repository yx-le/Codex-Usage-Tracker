namespace CodexUsageTracker.App;

public sealed class QuotaRing : FrameworkElement
{
    public static readonly DependencyProperty FiveProperty = DependencyProperty.Register(nameof(Five), typeof(double), typeof(QuotaRing), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty WeekProperty = DependencyProperty.Register(nameof(Week), typeof(double), typeof(QuotaRing), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Five { get => (double)GetValue(FiveProperty); set => SetValue(FiveProperty, value); }
    public double Week { get => (double)GetValue(WeekProperty); set => SetValue(WeekProperty, value); }
    public static readonly DependencyProperty FiveBrushProperty = DependencyProperty.Register(nameof(FiveBrush), typeof(Brush), typeof(QuotaRing), new FrameworkPropertyMetadata(Brushes.MediumAquamarine, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty WeekBrushProperty = DependencyProperty.Register(nameof(WeekBrush), typeof(Brush), typeof(QuotaRing), new FrameworkPropertyMetadata(Brushes.CornflowerBlue, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FiveExhaustedProperty = DependencyProperty.Register(nameof(FiveExhausted), typeof(bool), typeof(QuotaRing), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty WeekExhaustedProperty = DependencyProperty.Register(nameof(WeekExhausted), typeof(bool), typeof(QuotaRing), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush FiveBrush { get => (Brush)GetValue(FiveBrushProperty); set => SetValue(FiveBrushProperty, value); }
    public Brush WeekBrush { get => (Brush)GetValue(WeekBrushProperty); set => SetValue(WeekBrushProperty, value); }
    public bool FiveExhausted { get => (bool)GetValue(FiveExhaustedProperty); set => SetValue(FiveExhaustedProperty, value); }
    public bool WeekExhausted { get => (bool)GetValue(WeekExhaustedProperty); set => SetValue(WeekExhaustedProperty, value); }
    protected override void OnRender(DrawingContext drawing)
    {
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radius = Math.Min(ActualWidth, ActualHeight) / 2 - 5;
        DrawRing(drawing, center, radius, Five, FiveBrush, 5, FiveExhausted);
        DrawRing(drawing, center, radius - 9, Week, WeekBrush, 3, WeekExhausted);
    }
    private void DrawRing(DrawingContext drawing, Point center, double radius, double percent, Brush brush, double thickness, bool exhausted)
    {
        if (exhausted) { drawing.DrawEllipse(null, new Pen(brush, thickness), center, radius, radius); return; }
        drawing.DrawEllipse(null, new Pen((Brush)FindResource("BorderBrush"), thickness), center, radius, radius);
        if (percent <= 0) return;
        if (percent >= 100) { drawing.DrawEllipse(null, new Pen(brush, thickness), center, radius, radius); return; }
        var radians = percent / 100 * Math.PI * 2;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(center.X, center.Y - radius), false, false);
            context.ArcTo(new Point(center.X + radius * Math.Sin(radians), center.Y - radius * Math.Cos(radians)), new Size(radius, radius), 0, percent > 50, SweepDirection.Clockwise, true, false);
        }
        drawing.DrawGeometry(null, new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, geometry);
    }
}
