using BazookaLens.Diagnostics;

namespace BazookaLens.Tests;

public sealed class ReShadeEventBridgeTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    [InlineData(599, false)]
    [InlineData(600, true)]
    [InlineData(601, false)]
    [InlineData(1200, true)]
    public void ShouldLogModuleResolverHitThrottlesFrameRateCalls(long hitCount, bool expected)
    {
        Assert.Equal(expected, ReShadeEventBridgeLogPolicy.ShouldLogModuleResolverHit(hitCount));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    [InlineData(120, false)]
    [InlineData(599, false)]
    [InlineData(600, true)]
    [InlineData(601, false)]
    [InlineData(1200, true)]
    public void ShouldLogHighFrequencyEventThrottlesFrameRateEvents(long eventCount, bool expected)
    {
        Assert.Equal(expected, ReShadeEventBridgeLogPolicy.ShouldLogHighFrequencyEvent(eventCount));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    [InlineData(119, false)]
    [InlineData(120, true)]
    [InlineData(121, false)]
    public void ShouldLogLowFrequencyEventKeepsReloadDiagnosticsVisible(long eventCount, bool expected)
    {
        Assert.Equal(expected, ReShadeEventBridgeLogPolicy.ShouldLogLowFrequencyEvent(eventCount));
    }

    [Fact]
    public void ShouldResolveAddonModuleHandleAsOwnModule()
    {
        var addonModuleHandle = new IntPtr(0x2000);
        var callbackPointers = new[] { new IntPtr(0x3000) };

        Assert.True(ReShadeEventBridgeModuleResolver.ShouldResolve(addonModuleHandle, addonModuleHandle, callbackPointers));
    }

    [Fact]
    public void ShouldResolveCallbackPointersAsAddonModule()
    {
        var addonModuleHandle = new IntPtr(0x2000);
        var callbackPointers = new[] { new IntPtr(0x3000) };

        Assert.True(ReShadeEventBridgeModuleResolver.ShouldResolve(new IntPtr(0x3000), addonModuleHandle, callbackPointers));
    }

    [Fact]
    public void ShouldNotResolveUnrelatedPointers()
    {
        var addonModuleHandle = new IntPtr(0x2000);
        var callbackPointers = new[] { new IntPtr(0x3000) };

        Assert.False(ReShadeEventBridgeModuleResolver.ShouldResolve(new IntPtr(0x4000), addonModuleHandle, callbackPointers));
    }

    [Fact]
    public void AppendStoppedFooterPreservesPreStopSnapshotAndShowsAfterStopState()
    {
        const string preStopSummary = "ReShade event bridge stopping snapshot before unregister.\r\nActive=True\r\nTotalEventCount=1150\r\n";

        var result = ReShadeEventBridgeStatusFormatter.AppendStoppedFooter(preStopSummary, activeAfterStop: false);

        Assert.Contains("Active=True", result);
        Assert.Contains("TotalEventCount=1150", result);
        Assert.Contains("Stopped=True", result);
        Assert.Contains("ActiveAfterStop=False", result);
    }

    [Fact]
    public void ResourceViewInspectionFormatsSuccessfulTextureDescriptor()
    {
        var descriptor = ReShadeResourceViewInspection.Success(
            0x1111,
            0x2222,
            0x3333,
            "DXGI_FORMAT_R8G8B8A8_UNORM",
            width: 5120,
            height: 2880,
            sampleCount: 1,
            sampleQuality: 0);

        var result = descriptor.ToString();

        Assert.Equal(
            "View=0x1111, Resource=0x2222, Texture=0x3333, Texture=5120x2880, Format=DXGI_FORMAT_R8G8B8A8_UNORM, SampleCount=1, SampleQuality=0",
            result);
    }

    [Fact]
    public void ResourceViewInspectionFormatsFailedInspection()
    {
        var descriptor = ReShadeResourceViewInspection.Failure(0x1111, "QueryInterface failed");

        var result = descriptor.ToString();

        Assert.Equal("View=0x1111, Error=QueryInterface failed", result);
    }
}
