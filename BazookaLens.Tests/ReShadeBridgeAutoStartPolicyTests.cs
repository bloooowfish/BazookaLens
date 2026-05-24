using BazookaLens.Capture;
using BazookaLens.Diagnostics;

namespace BazookaLens.Tests;

public sealed class ReShadeBridgeAutoStartPolicyTests
{
    [Fact]
    public void AutoStartsForInactiveScaledAfterCaptures()
    {
        Assert.True(ReShadeBridgeAutoStartPolicy.ShouldAutoStart(CaptureTiming.AfterImGui, scale: 2.0, bridgeActive: false));
        Assert.True(ReShadeBridgeAutoStartPolicy.ShouldAutoStart(CaptureTiming.AfterImGui, scale: 0.75, bridgeActive: false));
    }

    [Fact]
    public void SkipsAutoStartForUnscaledBeforeOrAlreadyActiveCaptures()
    {
        Assert.False(ReShadeBridgeAutoStartPolicy.ShouldAutoStart(CaptureTiming.AfterImGui, scale: 1.0, bridgeActive: false));
        Assert.False(ReShadeBridgeAutoStartPolicy.ShouldAutoStart(CaptureTiming.BeforeImGui, scale: 2.0, bridgeActive: false));
        Assert.False(ReShadeBridgeAutoStartPolicy.ShouldAutoStart(CaptureTiming.AfterImGui, scale: 2.0, bridgeActive: true));
    }
}
