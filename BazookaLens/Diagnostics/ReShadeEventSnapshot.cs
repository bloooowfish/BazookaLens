namespace BazookaLens.Diagnostics;

internal readonly record struct ReShadeEventSnapshot(
    long InitEffectRuntime,
    long ReloadedEffects,
    long FinishEffects,
    long SetCurrentPresetPath,
    long LastInitEffectRuntimeSequence,
    long LastReloadedEffectsSequence,
    long LastFinishEffectsSequence,
    long LastSetCurrentPresetPathSequence);

internal readonly record struct ReShadeRestoreStabilizationDecision(
    bool Ready,
    ReShadeRestoreStabilizationReason Reason);

internal readonly record struct ReShadeRestoreStabilizationWaitResult(
    bool Ready,
    int Ticks,
    ReShadeRestoreStabilizationReason Reason,
    ReShadeEventSnapshot Snapshot);

internal enum ReShadeRestoreStabilizationReason
{
    NoPresetPathObservedBeforeRestore,
    WaitingForPresetPathAfterRestore,
    WaitingForReloadedEffectsAfterPresetPath,
    PresetPathReloadedAfterRestore,
}

internal static class ReShadeRestoreStabilizationPolicy
{
    public static ReShadeRestoreStabilizationDecision Decide(
        ReShadeEventSnapshot beforeRestore,
        ReShadeEventSnapshot current)
    {
        if (beforeRestore.SetCurrentPresetPath <= 0)
        {
            return new ReShadeRestoreStabilizationDecision(
                Ready: true,
                ReShadeRestoreStabilizationReason.NoPresetPathObservedBeforeRestore);
        }

        if (current.SetCurrentPresetPath <= beforeRestore.SetCurrentPresetPath)
        {
            return new ReShadeRestoreStabilizationDecision(
                Ready: false,
                ReShadeRestoreStabilizationReason.WaitingForPresetPathAfterRestore);
        }

        if (current.LastReloadedEffectsSequence <= current.LastSetCurrentPresetPathSequence)
        {
            return new ReShadeRestoreStabilizationDecision(
                Ready: false,
                ReShadeRestoreStabilizationReason.WaitingForReloadedEffectsAfterPresetPath);
        }

        return new ReShadeRestoreStabilizationDecision(
            Ready: true,
            ReShadeRestoreStabilizationReason.PresetPathReloadedAfterRestore);
    }
}
