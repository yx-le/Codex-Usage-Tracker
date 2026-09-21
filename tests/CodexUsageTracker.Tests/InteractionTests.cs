using CodexUsageTracker.Core;
using Xunit;

namespace CodexUsageTracker.Tests;

public class InteractionTests
{
    [Theory]
    [InlineData(0, QuotaStatus.Exhausted)] [InlineData(10, QuotaStatus.Critical)]
    [InlineData(11, QuotaStatus.Warning)] [InlineData(25, QuotaStatus.Warning)] [InlineData(26, QuotaStatus.Healthy)]
    public void QuotaColorsFollowRemainingQuota(double remaining, QuotaStatus expected)
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(expected, QuotaStatusPolicy.Evaluate(new(100 - remaining, 300, now.AddHours(1)), false, new(), now));
    }
    [Fact] public void ColorsRespectCustomThresholdsAndUnknownStates()
    {
        var now = DateTimeOffset.UtcNow; var settings = new TrackerSettings { WarningPercent = 40, CriticalPercent = 20 };
        Assert.Equal(QuotaStatus.Critical, QuotaStatusPolicy.Evaluate(new(80, 300, null), false, settings, now));
        Assert.Equal(QuotaStatus.Warning, QuotaStatusPolicy.Evaluate(new(65, 300, null), false, settings, now));
        Assert.Equal(QuotaStatus.Stale, QuotaStatusPolicy.Evaluate(new(100, 300, null), true, settings, now));
        Assert.Equal(QuotaStatus.Unknown, QuotaStatusPolicy.Evaluate(null, false, settings, now));
        Assert.Equal(QuotaStatus.ResetDue, QuotaStatusPolicy.Evaluate(new(100, 300, now), false, settings, now));
    }
    [Fact] public void ResetTriggersImmediatelyThenBacksOffUntilServerUpdates()
    {
        var now = DateTimeOffset.UtcNow; var schedule = new ResetRefreshSchedule();
        var sample = new UsageSnapshot(now, "Codex app-server", new(100, 300, now.AddSeconds(1)), null);
        Assert.False(schedule.ShouldRefresh(sample, now));
        Assert.True(schedule.ShouldRefresh(sample, now.AddSeconds(1)));
        Assert.False(schedule.ShouldRefresh(sample, now.AddSeconds(2)));
        Assert.True(schedule.ShouldRefresh(sample, now.AddSeconds(16)));
        Assert.False(schedule.ShouldRefresh(sample, now.AddSeconds(17)));
        Assert.True(schedule.ShouldRefresh(sample, now.AddSeconds(76)));
        var reset = sample with { FiveHour = new(0, 300, now.AddHours(5)) };
        Assert.False(schedule.ShouldRefresh(reset, now.AddSeconds(77)));
        Assert.True(schedule.ShouldRefresh(reset, now.AddHours(5)));
    }
    [Fact] public void WeeklyResetAlsoTriggersAndMissingResetDoesNot()
    {
        var now = DateTimeOffset.UtcNow; var schedule = new ResetRefreshSchedule();
        Assert.False(schedule.ShouldRefresh(new(now, "N/A", null, null), now));
        Assert.True(schedule.ShouldRefresh(new(now, "Local", null, new(100, 10080, now)), now));
    }
    [Theory]
    [InlineData(0, 0)] [InlineData(0, 880)] [InlineData(1824, 0)] [InlineData(1824, 880)] [InlineData(800, 400)]
    public void PanelStaysInsideWorkAreaAndDoesNotCoverWidget(double x, double y)
    {
        var work = new LayoutRect(0, 0, 1920, 1040); var widget = new LayoutRect(x, y, 96, 96);
        var panel = PanelPlacement.Beside(work, widget, 420, 790);
        AssertInsideAndSeparate(work, widget, panel);
        if (x == 0) Assert.True(panel.Left >= widget.Right + 12);
        if (x == 1824) Assert.True(panel.Right <= widget.Left - 12);
    }
    [Fact] public void HandlesNegativeMonitorCoordinatesAndCompactScreens()
    {
        var work = new LayoutRect(-1280, 0, 1280, 720); var widget = new LayoutRect(-1280, 300, 96, 96);
        AssertInsideAndSeparate(work, widget, PanelPlacement.Beside(work, widget, 420, 790));
        var small = new LayoutRect(0, 0, 600, 700); var centered = new LayoutRect(260, 300, 96, 96);
        AssertInsideAndSeparate(small, centered, PanelPlacement.Beside(small, centered, 420, 790));
    }
    private static void AssertInsideAndSeparate(LayoutRect work, LayoutRect widget, LayoutRect panel)
    {
        Assert.True(panel.Left >= work.Left && panel.Top >= work.Top && panel.Right <= work.Right && panel.Bottom <= work.Bottom);
        Assert.True(panel.Right <= widget.Left || panel.Left >= widget.Right || panel.Bottom <= widget.Top || panel.Top >= widget.Bottom);
    }
}
