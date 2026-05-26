using System.Numerics;

namespace BazookaLens.UI;

internal readonly record struct UiScreenRect(float X, float Y, float Width, float Height)
{
    public float Right => this.X + this.Width;

    public float Bottom => this.Y + this.Height;

    public static UiScreenRect FromPositionAndSize(Vector2 position, Vector2 size)
    {
        return new UiScreenRect(position.X, position.Y, size.X, size.Y);
    }

    public static UiScreenRect FromMinMax(Vector2 min, Vector2 max)
    {
        return new UiScreenRect(min.X, min.Y, max.X - min.X, max.Y - min.Y);
    }

    public bool Contains(Vector2 point)
    {
        return point.X >= this.X &&
            point.X <= this.Right &&
            point.Y >= this.Y &&
            point.Y <= this.Bottom;
    }
}

internal readonly record struct RegionOverlayFrameDecision(
    bool ShouldCloseBeforeDraw,
    bool ShouldDrawOverlay,
    bool NextWasRightPressed);

internal static class RegionOverlayInput
{
    public const float DefaultAnchorSize = 24f;
    public const float DefaultAnchorInset = 0f;

    public static RegionDragHandle? ResolveHandle(
        Vector2 mouse,
        UiScreenRect region,
        float anchorSize = DefaultAnchorSize,
        float anchorInset = DefaultAnchorInset)
    {
        foreach (var (zone, handle) in CreateAnchorZones(region, anchorSize, anchorInset))
        {
            if (zone.Contains(mouse))
                return handle;
        }

        return region.Contains(mouse)
            ? RegionDragHandle.Move
            : null;
    }

    public static (UiScreenRect Zone, RegionDragHandle Handle)[] CreateAnchorZones(
        UiScreenRect region,
        float anchorSize = DefaultAnchorSize,
        float anchorInset = DefaultAnchorInset)
    {
        var maxSize = MathF.Max(1f, MathF.Min(region.Width, region.Height));
        var size = MathF.Min(MathF.Max(1f, anchorSize), maxSize);
        var requestedInset = MathF.Max(0f, anchorInset);
        var insetX = MathF.Min(requestedInset, MathF.Max(0f, (region.Width - size) / 2f));
        var insetY = MathF.Min(requestedInset, MathF.Max(0f, (region.Height - size) / 2f));
        var left = region.X + insetX;
        var right = region.X + region.Width - insetX - size;
        var top = region.Y + insetY;
        var bottom = region.Y + region.Height - insetY - size;

        return
        [
            (new UiScreenRect(left, top, size, size), RegionDragHandle.TopLeft),
            (new UiScreenRect(right, top, size, size), RegionDragHandle.TopRight),
            (new UiScreenRect(left, bottom, size, size), RegionDragHandle.BottomLeft),
            (new UiScreenRect(right, bottom, size, size), RegionDragHandle.BottomRight),
        ];
    }

    public static UiScreenRect[] CreateAnchorBracketSegments(
        UiScreenRect zone,
        RegionDragHandle handle,
        float thickness)
    {
        var line = MathF.Min(MathF.Max(1f, thickness), MathF.Min(zone.Width, zone.Height));
        var left = new UiScreenRect(zone.X, zone.Y, line, zone.Height);
        var right = new UiScreenRect(zone.Right - line, zone.Y, line, zone.Height);
        var top = new UiScreenRect(zone.X, zone.Y, zone.Width, line);
        var bottom = new UiScreenRect(zone.X, zone.Bottom - line, zone.Width, line);

        return handle switch
        {
            RegionDragHandle.TopLeft => [top, left],
            RegionDragHandle.TopRight => [top, right],
            RegionDragHandle.BottomLeft => [bottom, left],
            RegionDragHandle.BottomRight => [bottom, right],
            _ => [],
        };
    }

    public static bool ShouldCloseFromRightClick(bool rightPressed, bool wasRightPressed)
    {
        return rightPressed && !wasRightPressed;
    }

    public static RegionOverlayFrameDecision DecideCloseBeforeDraw(bool rightPressed, bool wasRightPressed)
    {
        var shouldClose = ShouldCloseFromRightClick(rightPressed, wasRightPressed);
        return new RegionOverlayFrameDecision(
            shouldClose,
            ShouldDrawOverlay: !shouldClose,
            NextWasRightPressed: shouldClose ? false : rightPressed);
    }
}
