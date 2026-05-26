using BazookaLens.Capture;
using BazookaLens.Diagnostics;

namespace BazookaLens.Tests;

public sealed class ReShadePostEffectsCapturePolicyTests
{
    [Fact]
    public void UsesConservativeTimeoutForSlowReShadePostResizeRecovery()
    {
        Assert.Equal(TimeSpan.FromSeconds(12), ReShadePostEffectsCapturePolicy.CaptureTimeout);
    }

    [Fact]
    public void ArmsPostEffectsCaptureAfterSettleWhenFinishEffectsAdvanced()
    {
        var before = new ReShadeEventCounts(InitEffectRuntime: 1, ReloadedEffects: 1, BeginEffects: 10, FinishEffects: 10);
        var after = new ReShadeEventCounts(InitEffectRuntime: 2, ReloadedEffects: 2, BeginEffects: 12, FinishEffects: 12);

        Assert.True(ReShadePostEffectsCapturePolicy.ShouldArmPostEffectsCaptureAfterSettle(
            CaptureTiming.AfterImGui,
            bridgeActive: true,
            before,
            after));
    }

    [Fact]
    public void TriesPostEffectsCaptureWithoutResizeForActiveAfterCaptures()
    {
        Assert.True(ReShadePostEffectsCapturePolicy.ShouldTryPostEffectsCaptureWithoutResize(
            CaptureTiming.AfterImGui,
            bridgeActive: true));
    }

    [Fact]
    public void SkipsPostEffectsCaptureWithoutResizeForInactiveOrBeforeCaptures()
    {
        Assert.False(ReShadePostEffectsCapturePolicy.ShouldTryPostEffectsCaptureWithoutResize(
            CaptureTiming.AfterImGui,
            bridgeActive: false));
        Assert.False(ReShadePostEffectsCapturePolicy.ShouldTryPostEffectsCaptureWithoutResize(
            CaptureTiming.BeforeImGui,
            bridgeActive: true));
    }

    [Fact]
    public void SkipsArmingPostEffectsCaptureAfterSettleWhenBridgeIsInactive()
    {
        var before = new ReShadeEventCounts(InitEffectRuntime: 1, ReloadedEffects: 1, BeginEffects: 10, FinishEffects: 10);
        var after = new ReShadeEventCounts(InitEffectRuntime: 1, ReloadedEffects: 1, BeginEffects: 11, FinishEffects: 11);

        Assert.False(ReShadePostEffectsCapturePolicy.ShouldArmPostEffectsCaptureAfterSettle(
            CaptureTiming.AfterImGui,
            bridgeActive: false,
            before,
            after));
    }

    [Fact]
    public void SkipsArmingPostEffectsCaptureAfterSettleBeforeImGui()
    {
        var before = new ReShadeEventCounts(InitEffectRuntime: 1, ReloadedEffects: 1, BeginEffects: 10, FinishEffects: 10);
        var after = new ReShadeEventCounts(InitEffectRuntime: 1, ReloadedEffects: 1, BeginEffects: 11, FinishEffects: 11);

        Assert.False(ReShadePostEffectsCapturePolicy.ShouldArmPostEffectsCaptureAfterSettle(
            CaptureTiming.BeforeImGui,
            bridgeActive: true,
            before,
            after));
    }

    [Fact]
    public void DoesNotArmPostEffectsCaptureAfterSettleWhenFinishEffectsDidNotAdvance()
    {
        var before = new ReShadeEventCounts(InitEffectRuntime: 6, ReloadedEffects: 12, BeginEffects: 1309, FinishEffects: 1309);
        var after = new ReShadeEventCounts(InitEffectRuntime: 7, ReloadedEffects: 14, BeginEffects: 1309, FinishEffects: 1309);

        Assert.False(ReShadePostEffectsCapturePolicy.ShouldArmPostEffectsCaptureAfterSettle(
            CaptureTiming.AfterImGui,
            bridgeActive: true,
            before,
            after));
    }

}
