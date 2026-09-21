using CodexUsageTracker.Core;
using Xunit;

namespace CodexUsageTracker.Tests;

public class WidgetDragTests
{
    private static WidgetDragGesture Start(double scale = 1)
    {
        var gesture = new WidgetDragGesture();
        gesture.Begin(500, 400, 100, 200, scale, scale, 4, 4);
        return gesture;
    }

    [Fact] public void SmallPointerJitterRemainsAClick()
    {
        var gesture = Start();
        Assert.Null(gesture.Move(502, 401));
        Assert.False(gesture.End());
    }

    [Fact] public void DragUsesOriginalScreenPositionAcrossRepeatedMoves()
    {
        var gesture = Start();
        Assert.Equal((120d, 220d), gesture.Move(520, 420)!.Value);
        Assert.Equal((150d, 190d), gesture.Move(550, 390)!.Value);
        Assert.True(gesture.End());
        Assert.Null(gesture.Move(600, 500));
    }

    [Theory] [InlineData(1)] [InlineData(1.5)] [InlineData(2)]
    public void ScreenPixelsConvertToWindowDips(double scale)
    {
        var gesture = Start(scale);
        Assert.Equal((110d, 205d), gesture.Move(500 + 10 * scale, 400 + 5 * scale)!.Value);
    }

    [Fact] public void ReturningToStartAfterDraggingDoesNotOpenDetails()
    {
        var gesture = Start();
        gesture.Move(550, 450); gesture.Move(500, 400);
        Assert.True(gesture.End());
        gesture.Begin(500, 400, 100, 200, 1, 1, 4, 4);
        Assert.False(gesture.End());
    }

    [Fact] public void SupportsDraggingToNegativeMonitorCoordinates()
    {
        var gesture = Start();
        Assert.Equal((-300d, -100d), gesture.Move(100, 100)!.Value);
        Assert.True(gesture.End());
    }
}
