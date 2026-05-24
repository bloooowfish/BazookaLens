using System;

namespace BazookaLens.UI;

internal enum GuideLineOrientation
{
    Vertical,
    Horizontal,
}

internal readonly record struct GuideLine
{
    public GuideLine(GuideLineOrientation orientation, float normalizedPosition)
    {
        if (normalizedPosition <= 0f || normalizedPosition >= 1f || float.IsNaN(normalizedPosition) || float.IsInfinity(normalizedPosition))
            throw new ArgumentOutOfRangeException(nameof(normalizedPosition), "Guide line position must be finite and between 0 and 1.");

        this.Orientation = orientation;
        this.NormalizedPosition = normalizedPosition;
    }

    public GuideLineOrientation Orientation { get; }

    public float NormalizedPosition { get; }
}
