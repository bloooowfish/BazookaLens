using BazookaLens.Diagnostics;

namespace BazookaLens.Tests;

public sealed class ReShadeRestoreStabilizationPolicyTests
{
    [Fact]
    public void ReadyWhenNoPresetPathWasObservedBeforeRestore()
    {
        var before = new ReShadeEventSnapshot(
            InitEffectRuntime: 1,
            ReloadedEffects: 1,
            FinishEffects: 120,
            SetCurrentPresetPath: 0,
            LastInitEffectRuntimeSequence: 1,
            LastReloadedEffectsSequence: 2,
            LastFinishEffectsSequence: 240,
            LastSetCurrentPresetPathSequence: 0);
        var current = before;

        var decision = ReShadeRestoreStabilizationPolicy.Decide(before, current);

        Assert.True(decision.Ready);
        Assert.Equal(ReShadeRestoreStabilizationReason.NoPresetPathObservedBeforeRestore, decision.Reason);
    }

    [Fact]
    public void WaitsForPresetPathAfterRestoreWhenPresetWasObservedBeforeRestore()
    {
        var before = PresetObservedSnapshot();
        var current = before with
        {
            InitEffectRuntime = 2,
            ReloadedEffects = 3,
            LastInitEffectRuntimeSequence = 731,
            LastReloadedEffectsSequence = 732,
        };

        var decision = ReShadeRestoreStabilizationPolicy.Decide(before, current);

        Assert.False(decision.Ready);
        Assert.Equal(ReShadeRestoreStabilizationReason.WaitingForPresetPathAfterRestore, decision.Reason);
    }

    [Fact]
    public void WaitsForReloadedEffectsAfterPostRestorePresetPath()
    {
        var before = PresetObservedSnapshot();
        var current = before with
        {
            SetCurrentPresetPath = 2,
            LastSetCurrentPresetPathSequence = 733,
            ReloadedEffects = 3,
            LastReloadedEffectsSequence = 732,
        };

        var decision = ReShadeRestoreStabilizationPolicy.Decide(before, current);

        Assert.False(decision.Ready);
        Assert.Equal(ReShadeRestoreStabilizationReason.WaitingForReloadedEffectsAfterPresetPath, decision.Reason);
    }

    [Fact]
    public void ReadyWhenPostRestorePresetPathIsFollowedByReloadedEffects()
    {
        var before = PresetObservedSnapshot();
        var current = before with
        {
            SetCurrentPresetPath = 2,
            ReloadedEffects = 4,
            LastSetCurrentPresetPathSequence = 733,
            LastReloadedEffectsSequence = 734,
        };

        var decision = ReShadeRestoreStabilizationPolicy.Decide(before, current);

        Assert.True(decision.Ready);
        Assert.Equal(ReShadeRestoreStabilizationReason.PresetPathReloadedAfterRestore, decision.Reason);
    }

    private static ReShadeEventSnapshot PresetObservedSnapshot()
    {
        return new ReShadeEventSnapshot(
            InitEffectRuntime: 1,
            ReloadedEffects: 2,
            FinishEffects: 360,
            SetCurrentPresetPath: 1,
            LastInitEffectRuntimeSequence: 435,
            LastReloadedEffectsSequence: 438,
            LastFinishEffectsSequence: 724,
            LastSetCurrentPresetPathSequence: 437);
    }
}
