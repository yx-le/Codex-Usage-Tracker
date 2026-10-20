using CodexUsageTracker.Core;
using Xunit;

namespace CodexUsageTracker.Tests;

public class EdgeBarTests
{
    [Theory]
    [InlineData("Left", 28, 132)]
    [InlineData("Right", 28, 132)]
    [InlineData("Top", 132, 28)]
    public void DetailsAreCenteredBesideOrBelowBar(string edge, double barWidth, double barHeight)
    {
        var work = new LayoutRect(-1920, 100, 1920, 980);
        var bar = EdgeBarPlacement.Place(work, edge, 0.5, barWidth, barHeight);
        var panel = PanelPlacement.FromEdgeBar(work, bar, edge, 360, 530);
        if (edge == "Top")
        {
            Assert.Equal(bar.Bottom + 12, panel.Top);
            Assert.Equal(bar.Left + bar.Width / 2, panel.Left + panel.Width / 2);
        }
        else
        {
            Assert.Equal(bar.Top + bar.Height / 2, panel.Top + panel.Height / 2);
            if (edge == "Left") Assert.Equal(bar.Right + 12, panel.Left);
            else Assert.Equal(bar.Left - 12, panel.Right);
        }
    }
    [Theory]
    [InlineData("Left", 0)] [InlineData("Left", 1)]
    [InlineData("Right", 0)] [InlineData("Right", 1)]
    [InlineData("Top", 0)] [InlineData("Top", 1)]
    public void EdgePopupClampsAtMonitorCornersWithoutCoveringBar(string edge, double offset)
    {
        var work = new LayoutRect(-1280, -200, 1280, 720);
        var bar = EdgeBarPlacement.Place(work, edge, offset, edge == "Top" ? 132 : 28, edge == "Top" ? 28 : 132);
        var panel = PanelPlacement.FromEdgeBar(work, bar, edge, 420, 900);
        Assert.True(panel.Left >= work.Left && panel.Right <= work.Right && panel.Top >= work.Top && panel.Bottom <= work.Bottom);
        if (edge == "Top") Assert.Equal(bar.Bottom + 12, panel.Top);
        else if (edge == "Left") Assert.Equal(bar.Right + 12, panel.Left);
        else Assert.Equal(bar.Left - 12, panel.Right);
    }
    [Fact] public void DesktopDisplaysHaveEqualOptInDefaults()
    {
        var settings = new TrackerSettings();
        Assert.False(settings.FloatingWidget); Assert.False(settings.EdgeBar); Assert.True(settings.TaskbarStatus);
    }
    [Theory]
    [InlineData(500, 10, "Top")]
    [InlineData(8, 400, "Left")]
    [InlineData(990, 400, "Right")]
    [InlineData(500, 790, "Left")]
    public void DragChoosesNearestSupportedEdge(double x, double y, string expected)
        => Assert.Equal(expected, EdgeBarPlacement.NearestEdge(new(0, 0, 1000, 800), x, y));
    [Theory]
    [InlineData("Top", 0)] [InlineData("Top", 1)]
    [InlineData("Left", 0)] [InlineData("Left", 1)]
    [InlineData("Right", 0)] [InlineData("Right", 1)]
    public void BarFitsSelectedEdgeAndLeavesRoomForDetails(string edge, double offset)
    {
        var work = new LayoutRect(-1920, 100, 1920, 980);
        var bar = EdgeBarPlacement.Place(work, edge, offset, edge == "Top" ? 132 : 28, edge == "Top" ? 28 : 132);
        Assert.True(bar.Left >= work.Left && bar.Right <= work.Right && bar.Top >= work.Top && bar.Bottom <= work.Bottom);
        if (edge == "Top") Assert.Equal(work.Top, bar.Top);
        if (edge == "Left") Assert.Equal(work.Left, bar.Left);
        if (edge == "Right") Assert.Equal(work.Right, bar.Right);
        var panel = PanelPlacement.Beside(work, bar, 420, 790);
        Assert.True(panel.Right <= bar.Left || panel.Left >= bar.Right || panel.Bottom <= bar.Top || panel.Top >= bar.Bottom);
    }
    [Fact] public void OutOfRangeOffsetIsClamped()
    {
        var work = new LayoutRect(0, 0, 1280, 720);
        Assert.Equal(0, EdgeBarPlacement.Place(work, "Top", -5, 150, 53).Left);
        Assert.Equal(720, EdgeBarPlacement.Place(work, "Right", 5, 58, 108).Bottom);
    }
    [Fact] public void DisplayPreferencesRoundTrip()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CodexUsageTracker.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(directory, "settings.json");
            new TrackerSettings { FloatingWidget = false, TrayPercentage = true, TrayQuota = "Weekly", EdgeBar = true, Edge = "Right", EdgeOffset = 0.8, EdgeMonitor = "secondary" }.Save(path);
            var saved = TrackerSettings.Load(path);
            Assert.False(saved.FloatingWidget); Assert.True(saved.EdgeBar); Assert.True(saved.TrayPercentage);
            Assert.Equal("Right", saved.Edge); Assert.Equal("Weekly", saved.TrayQuota); Assert.Equal(0.8, saved.EdgeOffset); Assert.Equal("secondary", saved.EdgeMonitor);
        }
        finally { Directory.Delete(directory, true); }
    }
}
