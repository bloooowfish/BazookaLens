using BazookaLens.Capture;
using BazookaLens.Diagnostics;

namespace BazookaLens.Tests;

public sealed class ReShadePostEffectsCaptureRegionTests
{
    [Fact]
    public void ResolveUsesFullTextureWhenRegionIsMissing()
    {
        var region = ReShadePostEffectsCaptureRegion.Resolve(null, textureWidth: 5120, textureHeight: 2880);

        Assert.Equal(new CaptureRegion(0, 0, 5120, 2880), region);
    }

    [Fact]
    public void ResolveRejectsRegionOutsideTexture()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ReShadePostEffectsCaptureRegion.Resolve(new CaptureRegion(5000, 100, 200, 100), textureWidth: 5120, textureHeight: 2880));

        Assert.Contains("beyond", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
