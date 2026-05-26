using BazookaLens.Capture;

namespace BazookaLens.Tests;

public sealed class CaptureUiHideTimingPolicyTests
{
    [Fact]
    public void WaitsAtLeastOneFrameworkTickAfterHidingGameUi()
    {
        Assert.True(CaptureUiHideTimingPolicy.GameUiHidePresentationDelayTicks >= 1);
    }
}
