using CodexUsageTracker.Core;
using Xunit;

namespace CodexUsageTracker.Tests;

public sealed class ReliabilityTests
{
    [Fact] public void ResetRetriesSurviveUnavailableAndPartialReadings()
    {
        var now = DateTimeOffset.UtcNow;
        var schedule = new ResetRefreshSchedule();
        Assert.True(schedule.ShouldRefresh(new(now, "Live", new(100, 300, now), null), now));
        Assert.False(schedule.ShouldRefresh(UsageSnapshot.Unavailable(now), now.AddSeconds(14)));
        Assert.True(schedule.ShouldRefresh(UsageSnapshot.Unavailable(now), now.AddSeconds(15)));
        Assert.True(schedule.ShouldRefresh(new(now, "Live", null, new(20, 10080, now.AddDays(4))), now.AddSeconds(75)));
        Assert.False(schedule.ShouldRefresh(new(now, "Live", new(0, 300, now.AddHours(5)), null), now.AddSeconds(135)));
        // An older fallback cannot re-arm the previous reset.
        Assert.False(schedule.ShouldRefresh(new(now.AddMinutes(-1), "Local", new(100, 300, now), null), now.AddSeconds(195)));
    }

    [Fact] public void EachQuotaWindowRetainsItsOwnPendingReset()
    {
        var now = DateTimeOffset.UtcNow; var schedule = new ResetRefreshSchedule();
        Assert.True(schedule.ShouldRefresh(new(now, "Live", new(100, 300, now), new(100, 10080, now)), now));
        Assert.True(schedule.ShouldRefresh(new(now, "Live", new(0, 300, now.AddHours(5)), null), now.AddSeconds(15)));
        Assert.False(schedule.ShouldRefresh(new(now, "Live", null, new(0, 10080, now.AddDays(7))), now.AddSeconds(75)));
    }

    [Fact] public void PendingRetriesExpireAfterEightDays()
    {
        var now = DateTimeOffset.UtcNow; var schedule = new ResetRefreshSchedule();
        Assert.True(schedule.ShouldRefresh(new(now, "Live", new(100, 300, now), null), now));
        Assert.False(schedule.ShouldRefresh(UsageSnapshot.Unavailable(now), now.AddDays(9)));
    }

    [Theory]
    [InlineData(1950, 100, 96)] // Gap above an offset secondary monitor.
    [InlineData(1800, 1020, 96)] // Primary taskbar.
    [InlineData(-1800, 100, 144)] // Disconnected screen / scaled widget.
    [InlineData(3100, 900, 192)] // Scaled widget at the right edge.
    public void RecoveredWidgetFitsOneActualWorkArea(double x, double y, double size)
    {
        LayoutRect[] areas = [new(0, 0, 1920, 1040), new(1920, 400, 1280, 680)];
        var result = WidgetPlacement.InsideNearestWorkArea(new(x, y, size, size), areas);
        Assert.Contains(areas, area => result.Left >= area.Left && result.Right <= area.Right && result.Top >= area.Top && result.Bottom <= area.Bottom);
    }

    [Fact] public void ValidPositionOnNegativeOriginMonitorDoesNotMove()
    {
        var widget = new LayoutRect(-1400, 200, 144, 144);
        Assert.Equal(widget, WidgetPlacement.InsideNearestWorkArea(widget, [new(-1600, -200, 1600, 1100), new(0, 0, 1920, 1040)]));
    }

}
