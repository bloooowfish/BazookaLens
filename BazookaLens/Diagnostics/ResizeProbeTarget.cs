using System;

namespace BazookaLens.Diagnostics;

internal sealed record ResizeProbeTarget(uint Width, uint Height)
{
    public static ResizeProbeTarget FromScale(uint sourceWidth, uint sourceHeight, double scale)
    {
        if (sourceWidth == 0)
            throw new ArgumentOutOfRangeException(nameof(sourceWidth), sourceWidth, "Source width must be greater than zero.");

        if (sourceHeight == 0)
            throw new ArgumentOutOfRangeException(nameof(sourceHeight), sourceHeight, "Source height must be greater than zero.");

        if (scale <= 0 || double.IsNaN(scale) || double.IsInfinity(scale))
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "Scale must be a positive finite number.");

        return new ResizeProbeTarget(
            checked((uint)Math.Round(sourceWidth * scale, MidpointRounding.AwayFromZero)),
            checked((uint)Math.Round(sourceHeight * scale, MidpointRounding.AwayFromZero)));
    }
}
