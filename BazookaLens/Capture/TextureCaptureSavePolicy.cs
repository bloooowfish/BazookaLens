using System;

namespace BazookaLens.Capture;

internal static class TextureCaptureSavePolicy
{
    public const string ReShadeFinishEffectsSource = "ReShadeFinishEffects";

    public static bool ShouldMakeOpaque(string captureSource)
    {
        return string.Equals(captureSource, ReShadeFinishEffectsSource, StringComparison.Ordinal);
    }
}
