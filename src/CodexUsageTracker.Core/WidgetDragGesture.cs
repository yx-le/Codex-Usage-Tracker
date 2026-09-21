namespace CodexUsageTracker.Core;

/// <summary>Separates clicks from drags using stable screen coordinates and DIP-aware movement.</summary>
public sealed class WidgetDragGesture
{
    private double screenX, screenY, left, top, scaleX, scaleY, thresholdX, thresholdY;
    public bool IsActive { get; private set; }
    public bool HasDragged { get; private set; }

    public void Begin(double pointerX, double pointerY, double windowLeft, double windowTop,
        double dpiScaleX, double dpiScaleY, double horizontalThreshold, double verticalThreshold)
    {
        screenX = pointerX; screenY = pointerY; left = windowLeft; top = windowTop;
        scaleX = dpiScaleX; scaleY = dpiScaleY;
        thresholdX = horizontalThreshold; thresholdY = verticalThreshold;
        IsActive = true; HasDragged = false;
    }

    public (double Left, double Top)? Move(double pointerX, double pointerY)
    {
        if (!IsActive) return null;
        var dx = (pointerX - screenX) / scaleX;
        var dy = (pointerY - screenY) / scaleY;
        if (!HasDragged && Math.Abs(dx) < thresholdX && Math.Abs(dy) < thresholdY) return null;
        HasDragged = true;
        return (left + dx, top + dy);
    }

    public bool End()
    {
        var moved = IsActive && HasDragged;
        IsActive = false; HasDragged = false;
        return moved;
    }
}
