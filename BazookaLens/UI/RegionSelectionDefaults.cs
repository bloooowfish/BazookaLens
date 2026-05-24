using BazookaLens.Capture;

namespace BazookaLens.UI;

internal static class RegionSelectionDefaults
{
    public const double DefaultViewportFraction = 0.75;

    public static CaptureRegion CreateCenteredRegion(
        int viewportWidth,
        int viewportHeight,
        double viewportFraction = DefaultViewportFraction)
    {
        if (viewportWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewportWidth), "Viewport width must be positive.");

        if (viewportHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewportHeight), "Viewport height must be positive.");

        if (viewportFraction <= 0 || viewportFraction > 1 || double.IsNaN(viewportFraction) || double.IsInfinity(viewportFraction))
            throw new ArgumentOutOfRangeException(nameof(viewportFraction), "Viewport fraction must be greater than 0 and at most 1.");

        var width = Math.Clamp(
            ScaleViewportDimension(viewportWidth, viewportFraction),
            Math.Min(RegionSelectionState.MinRegionSizePixels, viewportWidth),
            viewportWidth);
        var height = Math.Clamp(
            ScaleViewportDimension(viewportHeight, viewportFraction),
            Math.Min(RegionSelectionState.MinRegionSizePixels, viewportHeight),
            viewportHeight);

        return new CaptureRegion((viewportWidth - width) / 2, (viewportHeight - height) / 2, width, height);
    }

    public static CaptureRegion ClampRegion(CaptureRegion region, int viewportWidth, int viewportHeight)
    {
        var state = new RegionSelectionState(region.X, region.Y, region.Width, region.Height)
            .ApplyDrag(RegionDragHandle.Move, 0, 0, viewportWidth, viewportHeight);
        return new CaptureRegion(state.X, state.Y, state.Width, state.Height);
    }

    public static CaptureRegion CreateCenteredOrClampExisting(
        CaptureRegion? existingRegion,
        int viewportWidth,
        int viewportHeight)
    {
        return existingRegion is { } region
            ? ClampRegion(region, viewportWidth, viewportHeight)
            : CreateCenteredRegion(viewportWidth, viewportHeight);
    }

    private static int ScaleViewportDimension(int value, double scale)
    {
        var scaled = Math.Round(value * scale, MidpointRounding.AwayFromZero);
        if (scaled > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(scale), "Scaled viewport dimension exceeds the supported pixel range.");

        return (int)scaled;
    }
}
