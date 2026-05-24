using BazookaLens.Diagnostics;

namespace BazookaLens.Tests;

public sealed class ResizeProbeTargetTests
{
    [Theory]
    [InlineData(1920u, 1080u, 2.0, 3840u, 2160u)]
    [InlineData(2560u, 1440u, 1.5, 3840u, 2160u)]
    [InlineData(3440u, 1440u, 1.25, 4300u, 1800u)]
    public void FromScaleRoundsTargetDimensions(uint sourceWidth, uint sourceHeight, double scale, uint expectedWidth, uint expectedHeight)
    {
        var target = ResizeProbeTarget.FromScale(sourceWidth, sourceHeight, scale);

        Assert.Equal(expectedWidth, target.Width);
        Assert.Equal(expectedHeight, target.Height);
    }

    [Theory]
    [InlineData(0u, 1080u, 2.0)]
    [InlineData(1920u, 0u, 2.0)]
    [InlineData(1920u, 1080u, 0.0)]
    public void FromScaleRejectsInvalidInputs(uint sourceWidth, uint sourceHeight, double scale)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ResizeProbeTarget.FromScale(sourceWidth, sourceHeight, scale));
    }
}
