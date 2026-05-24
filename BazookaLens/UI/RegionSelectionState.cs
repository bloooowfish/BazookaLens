using System;

namespace BazookaLens.UI;

internal readonly record struct RegionSelectionState(int X, int Y, int Width, int Height)
{
    public const int MinSize = 16;
    public const int MinRegionSizePixels = MinSize;

    public RegionSelectionState ApplyDrag(
        RegionDragHandle handle,
        int deltaX,
        int deltaY,
        int viewportWidth,
        int viewportHeight)
    {
        ValidateViewport(viewportWidth, viewportHeight);

        var minWidth = Math.Min(MinSize, viewportWidth);
        var minHeight = Math.Min(MinSize, viewportHeight);

        return handle switch
        {
            RegionDragHandle.Move => this.Move(deltaX, deltaY, viewportWidth, viewportHeight, minWidth, minHeight),
            RegionDragHandle.TopLeft => this.ResizeTopLeft(deltaX, deltaY, viewportWidth, viewportHeight, minWidth, minHeight),
            RegionDragHandle.TopRight => this.ResizeTopRight(deltaX, deltaY, viewportWidth, viewportHeight, minWidth, minHeight),
            RegionDragHandle.BottomLeft => this.ResizeBottomLeft(deltaX, deltaY, viewportWidth, viewportHeight, minWidth, minHeight),
            RegionDragHandle.BottomRight => this.ResizeBottomRight(deltaX, deltaY, viewportWidth, viewportHeight, minWidth, minHeight),
            _ => throw new ArgumentOutOfRangeException(nameof(handle), handle, "Unknown region drag handle."),
        };
    }

    private RegionSelectionState Move(int deltaX, int deltaY, int viewportWidth, int viewportHeight, int minWidth, int minHeight)
    {
        var width = Clamp(Math.Max(this.Width, minWidth), minWidth, viewportWidth);
        var height = Clamp(Math.Max(this.Height, minHeight), minHeight, viewportHeight);
        var x = Clamp(this.X + deltaX, 0, viewportWidth - width);
        var y = Clamp(this.Y + deltaY, 0, viewportHeight - height);

        return new RegionSelectionState(x, y, width, height);
    }

    private RegionSelectionState ResizeTopLeft(int deltaX, int deltaY, int viewportWidth, int viewportHeight, int minWidth, int minHeight)
    {
        var right = Clamp(this.X + this.Width, minWidth, viewportWidth);
        var bottom = Clamp(this.Y + this.Height, minHeight, viewportHeight);
        var x = Clamp(this.X + deltaX, 0, right - minWidth);
        var y = Clamp(this.Y + deltaY, 0, bottom - minHeight);

        return new RegionSelectionState(x, y, right - x, bottom - y);
    }

    private RegionSelectionState ResizeTopRight(int deltaX, int deltaY, int viewportWidth, int viewportHeight, int minWidth, int minHeight)
    {
        var x = Clamp(this.X, 0, viewportWidth - minWidth);
        var bottom = Clamp(this.Y + this.Height, minHeight, viewportHeight);
        var right = Clamp(this.X + this.Width + deltaX, x + minWidth, viewportWidth);
        var y = Clamp(this.Y + deltaY, 0, bottom - minHeight);

        return new RegionSelectionState(x, y, right - x, bottom - y);
    }

    private RegionSelectionState ResizeBottomLeft(int deltaX, int deltaY, int viewportWidth, int viewportHeight, int minWidth, int minHeight)
    {
        var right = Clamp(this.X + this.Width, minWidth, viewportWidth);
        var y = Clamp(this.Y, 0, viewportHeight - minHeight);
        var x = Clamp(this.X + deltaX, 0, right - minWidth);
        var bottom = Clamp(this.Y + this.Height + deltaY, y + minHeight, viewportHeight);

        return new RegionSelectionState(x, y, right - x, bottom - y);
    }

    private RegionSelectionState ResizeBottomRight(int deltaX, int deltaY, int viewportWidth, int viewportHeight, int minWidth, int minHeight)
    {
        var x = Clamp(this.X, 0, viewportWidth - minWidth);
        var y = Clamp(this.Y, 0, viewportHeight - minHeight);
        var right = Clamp(this.X + this.Width + deltaX, x + minWidth, viewportWidth);
        var bottom = Clamp(this.Y + this.Height + deltaY, y + minHeight, viewportHeight);

        return new RegionSelectionState(x, y, right - x, bottom - y);
    }

    private static void ValidateViewport(int viewportWidth, int viewportHeight)
    {
        if (viewportWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewportWidth), "Viewport width must be positive.");

        if (viewportHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewportHeight), "Viewport height must be positive.");
    }

    private static int Clamp(int value, int min, int max)
    {
        return Math.Min(Math.Max(value, min), max);
    }
}
