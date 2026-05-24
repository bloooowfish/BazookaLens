namespace BazookaLens.Diagnostics;

internal readonly record struct ReShadeEventCounts(
    long InitEffectRuntime,
    long ReloadedEffects,
    long BeginEffects,
    long FinishEffects);
