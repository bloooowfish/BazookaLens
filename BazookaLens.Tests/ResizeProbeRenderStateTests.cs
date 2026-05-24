using BazookaLens.Diagnostics;

namespace BazookaLens.Tests;

public sealed class ResizeProbeRenderStateTests
{
    [Fact]
    public void MatchesTargetWhenAllRenderDimensionsMatch()
    {
        var state = CreateState(3840, 2160, 3840, 2160, 3840, 2160, 3840, 2160);

        Assert.True(state.MatchesTarget(new ResizeProbeTarget(3840, 2160)));
    }

    [Fact]
    public void MatchesTargetRequiresEveryObservedDimension()
    {
        var state = CreateState(3840, 2160, 1920, 1080, 3840, 2160, 3840, 2160);

        Assert.False(state.MatchesTarget(new ResizeProbeTarget(3840, 2160)));
    }

    [Fact]
    public void MatchesPresentationTargetIgnoresInternalRenderResolution()
    {
        var state = CreateState(5120, 2880, 5120, 2880, 5069, 2851, 5120, 2880);

        Assert.True(state.MatchesPresentationTarget(new ResizeProbeTarget(5120, 2880)));
        Assert.False(state.MatchesTarget(new ResizeProbeTarget(5120, 2880)));
    }

    [Fact]
    public void MatchesPresentationTargetStillRequiresPresentedDimensions()
    {
        var state = CreateState(5120, 2880, 2560, 1440, 5069, 2851, 5120, 2880);

        Assert.False(state.MatchesPresentationTarget(new ResizeProbeTarget(5120, 2880)));
    }

    [Fact]
    public void MatchesTargetRejectsMissingDimensions()
    {
        var state = CreateState(null, 2160, 3840, 2160, 3840, 2160, 3840, 2160);

        Assert.False(state.MatchesTarget(new ResizeProbeTarget(3840, 2160)));
    }

    [Fact]
    public void ToSummaryFormatsObservedDimensionsOnOneLine()
    {
        var state = CreateState(5120, 2880, 5120, 2880, 5069, 2851, 5120, 2880);

        Assert.Equal(
            "Device=5120x2880, SwapChain=5120x2880, Render=5069x2851, Viewport=5120x2880, RequestResolutionChange=<unavailable>",
            state.ToSummary());
    }

    private static ResizeProbeRenderState CreateState(
        uint? deviceWidth,
        uint? deviceHeight,
        uint? swapChainWidth,
        uint? swapChainHeight,
        uint? renderWidth,
        uint? renderHeight,
        uint? viewportWidth,
        uint? viewportHeight)
    {
        return new ResizeProbeRenderState(
            DateTimeOffset.UnixEpoch,
            nint.Zero,
            deviceWidth,
            deviceHeight,
            deviceWidth,
            deviceHeight,
            null,
            nint.Zero,
            nint.Zero,
            swapChainWidth,
            swapChainHeight,
            nint.Zero,
            nint.Zero,
            renderWidth,
            renderHeight,
            0,
            viewportWidth,
            viewportHeight);
    }
}
