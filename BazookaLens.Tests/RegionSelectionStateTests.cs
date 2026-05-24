using BazookaLens.Capture;
using BazookaLens.UI;

namespace BazookaLens.Tests;

public sealed class RegionSelectionStateTests
{
    [Fact]
    public void DefaultRegionUsesCenteredViewportFraction()
    {
        var region = RegionSelectionDefaults.CreateCenteredRegion(2560, 1440, 0.75);

        Assert.Equal(320, region.X);
        Assert.Equal(180, region.Y);
        Assert.Equal(1920, region.Width);
        Assert.Equal(1080, region.Height);
    }

    [Fact]
    public void ExistingRegionForEditingIsClampedInsteadOfReset()
    {
        var region = RegionSelectionDefaults.CreateCenteredOrClampExisting(
            new CaptureRegion(2400, 1300, 400, 300),
            2560,
            1440);

        Assert.Equal(new CaptureRegion(2160, 1140, 400, 300), region);
    }

    [Fact]
    public void MissingRegionForEditingUsesCenteredDefault()
    {
        var region = RegionSelectionDefaults.CreateCenteredOrClampExisting(null, 2560, 1440);

        Assert.Equal(new CaptureRegion(320, 180, 1920, 1080), region);
    }

    [Fact]
    public void OverlayInputDoesNotReservePluginWindowBounds()
    {
        var region = new UiScreenRect(50, 50, 800, 600);

        Assert.Equal(RegionDragHandle.TopLeft, RegionOverlayInput.ResolveHandle(new System.Numerics.Vector2(50, 50), region));
        Assert.Equal(RegionDragHandle.Move, RegionOverlayInput.ResolveHandle(new System.Numerics.Vector2(200, 200), region));
        Assert.Equal(RegionDragHandle.Move, RegionOverlayInput.ResolveHandle(new System.Numerics.Vector2(700, 300), region));
    }

    [Fact]
    public void OverlayInputOnlyHandlesRegionBodyAndCornerAnchors()
    {
        var region = new UiScreenRect(100, 100, 400, 300);

        Assert.Equal(RegionDragHandle.TopLeft, RegionOverlayInput.ResolveHandle(new System.Numerics.Vector2(100, 100), region));
        Assert.Equal(RegionDragHandle.TopRight, RegionOverlayInput.ResolveHandle(new System.Numerics.Vector2(500, 100), region));
        Assert.Equal(RegionDragHandle.BottomLeft, RegionOverlayInput.ResolveHandle(new System.Numerics.Vector2(100, 400), region));
        Assert.Equal(RegionDragHandle.BottomRight, RegionOverlayInput.ResolveHandle(new System.Numerics.Vector2(500, 400), region));
        Assert.Equal(RegionDragHandle.Move, RegionOverlayInput.ResolveHandle(new System.Numerics.Vector2(300, 250), region));
        Assert.Null(RegionOverlayInput.ResolveHandle(new System.Numerics.Vector2(50, 50), region));
    }

    [Fact]
    public void OverlayInputKeepsAnchorHitZoneInsideCornerBracketBounds()
    {
        var region = new UiScreenRect(100, 100, 400, 300);

        Assert.Equal(RegionDragHandle.TopLeft, RegionOverlayInput.ResolveHandle(new System.Numerics.Vector2(123, 123), region));
        Assert.Equal(RegionDragHandle.Move, RegionOverlayInput.ResolveHandle(new System.Numerics.Vector2(125, 125), region));
    }

    [Fact]
    public void OverlayAnchorBracketSegmentsStayInsideAnchorZone()
    {
        var zone = new UiScreenRect(100, 100, 24, 24);

        foreach (var handle in new[]
                 {
                     RegionDragHandle.TopLeft,
                     RegionDragHandle.TopRight,
                     RegionDragHandle.BottomLeft,
                     RegionDragHandle.BottomRight,
                 })
        {
            var segments = RegionOverlayInput.CreateAnchorBracketSegments(zone, handle, 3f);

            Assert.All(segments, segment =>
            {
                Assert.True(segment.X >= zone.X);
                Assert.True(segment.Y >= zone.Y);
                Assert.True(segment.Right <= zone.Right);
                Assert.True(segment.Bottom <= zone.Bottom);
            });
        }
    }

    [Fact]
    public void OverlayRightClickCloseUsesEdgeTrigger()
    {
        Assert.True(RegionOverlayInput.ShouldCloseFromRightClick(rightPressed: true, wasRightPressed: false));
        Assert.False(RegionOverlayInput.ShouldCloseFromRightClick(rightPressed: true, wasRightPressed: true));
        Assert.False(RegionOverlayInput.ShouldCloseFromRightClick(rightPressed: false, wasRightPressed: false));
    }

    [Fact]
    public void MoveClampsRegionInsideViewport()
    {
        var state = new RegionSelectionState(90, 80, 40, 30);

        var moved = state.ApplyDrag(RegionDragHandle.Move, 100, 100, 160, 120);

        Assert.Equal(new RegionSelectionState(120, 90, 40, 30), moved);
    }

    [Theory]
    [InlineData((int)RegionDragHandle.TopLeft, 30, 20, 30, 30)]
    [InlineData((int)RegionDragHandle.TopRight, 20, 20, 50, 30)]
    [InlineData((int)RegionDragHandle.BottomLeft, 30, 10, 30, 50)]
    [InlineData((int)RegionDragHandle.BottomRight, 20, 10, 50, 50)]
    public void ResizePreservesOppositeCorner(int handle, int expectedX, int expectedY, int expectedWidth, int expectedHeight)
    {
        var state = new RegionSelectionState(20, 10, 40, 40);

        var resized = state.ApplyDrag((RegionDragHandle)handle, 10, 10, 100, 100);

        Assert.Equal(new RegionSelectionState(expectedX, expectedY, expectedWidth, expectedHeight), resized);
    }

    [Fact]
    public void ResizeClampsToMinimumRegionSize()
    {
        var state = new RegionSelectionState(20, 20, 40, 40);

        var resized = state.ApplyDrag(RegionDragHandle.BottomRight, -100, -100, 100, 100);

        Assert.Equal(new RegionSelectionState(20, 20, 16, 16), resized);
    }

    [Fact]
    public void ResizeClampsInsideViewport()
    {
        var state = new RegionSelectionState(20, 20, 40, 40);

        var resized = state.ApplyDrag(RegionDragHandle.TopLeft, -100, -100, 100, 100);

        Assert.Equal(new RegionSelectionState(0, 0, 60, 60), resized);
    }

    [Fact]
    public void ViewportSmallerThanMinimumUsesLargestAvailableRegion()
    {
        var state = new RegionSelectionState(0, 0, 10, 10);

        var resized = state.ApplyDrag(RegionDragHandle.BottomRight, 10, 10, 12, 8);

        Assert.Equal(new RegionSelectionState(0, 0, 12, 8), resized);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-1, 100)]
    [InlineData(100, -1)]
    public void DragRejectsInvalidViewportInputs(int viewportWidth, int viewportHeight)
    {
        var state = new RegionSelectionState(0, 0, 16, 16);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            state.ApplyDrag(RegionDragHandle.Move, 0, 0, viewportWidth, viewportHeight));
    }
}
