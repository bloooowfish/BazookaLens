using System;

namespace BazookaLens.Capture;

internal static class TextureCaptureSavePolicy
{
    public const string ReShadeFinishEffectsSource = "ReShadeFinishEffects";

    public static bool ShouldMakeOpaque(string captureSource, CaptureImageFormat imageFormat)
    {
        return imageFormat == CaptureImageFormat.Png &&
            string.Equals(captureSource, ReShadeFinishEffectsSource, StringComparison.Ordinal);
    }
}
