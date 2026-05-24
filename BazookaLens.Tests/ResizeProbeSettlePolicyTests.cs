using BazookaLens.Diagnostics;

namespace BazookaLens.Tests;

public sealed class ResizeProbeSettlePolicyTests
{
    [Fact]
    public void DecideWaitsUntilMinimumTicksHaveElapsed()
    {
        var decision = ResizeProbeSettlePolicy.Decide(
            elapsedTicks: 59,
            consecutiveStableTicks: 59,
            minimumSettleTicks: 60,
            requiredStableTicks: 5);

        Assert.False(decision.Ready);
        Assert.Equal(ResizeProbeSettleWaitReason.WaitingForMinimumTicks, decision.Reason);
    }

    [Fact]
    public void DecideWaitsUntilEnoughConsecutiveStableTicksHaveElapsed()
    {
        var decision = ResizeProbeSettlePolicy.Decide(
            elapsedTicks: 60,
            consecutiveStableTicks: 4,
            minimumSettleTicks: 60,
            requiredStableTicks: 5);

        Assert.False(decision.Ready);
        Assert.Equal(ResizeProbeSettleWaitReason.WaitingForStableTicks, decision.Reason);
    }

    [Fact]
    public void DecideReturnsReadyAfterMinimumTicksAndStableTicks()
    {
        var decision = ResizeProbeSettlePolicy.Decide(
            elapsedTicks: 60,
            consecutiveStableTicks: 5,
            minimumSettleTicks: 60,
            requiredStableTicks: 5);

        Assert.True(decision.Ready);
        Assert.Equal(ResizeProbeSettleWaitReason.Ready, decision.Reason);
    }
}
