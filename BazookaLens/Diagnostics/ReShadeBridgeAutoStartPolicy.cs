using System;

using BazookaLens.Capture;

namespace BazookaLens.Diagnostics;

internal static class ReShadeBridgeAutoStartPolicy
{
    public static bool ShouldAutoStart(CaptureTiming timing, double scale, bool bridgeActive)
    {
        return timing == CaptureTiming.AfterImGui &&
               Math.Abs(scale - 1.0) > double.Epsilon &&
               !bridgeActive;
    }
}
