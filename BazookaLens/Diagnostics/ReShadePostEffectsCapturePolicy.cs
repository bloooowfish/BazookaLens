using System;

using BazookaLens.Capture;

namespace BazookaLens.Diagnostics;

internal static class ReShadePostEffectsCapturePolicy
{
    public static TimeSpan CaptureTimeout { get; } = TimeSpan.FromSeconds(12);

    public static bool ShouldArmPostEffectsCaptureAfterSettle(
        CaptureTiming timing,
        bool bridgeActive,
        ReShadeEventCounts beforeSettle,
        ReShadeEventCounts afterSettle)
    {
        return timing == CaptureTiming.AfterImGui &&
               bridgeActive &&
               afterSettle.FinishEffects > beforeSettle.FinishEffects;
    }
}
