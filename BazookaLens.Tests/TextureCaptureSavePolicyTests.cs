using BazookaLens.Capture;

namespace BazookaLens.Tests;

public sealed class TextureCaptureSavePolicyTests
{
    [Theory]
    [InlineData("ReShadeFinishEffects", true)]
    [InlineData("Viewport", false)]
    [InlineData("Other", false)]
    public void MakesOnlyReShadeFinishEffectsCapturesOpaque(string captureSource, bool expected)
    {
        Assert.Equal(expected, TextureCaptureSavePolicy.ShouldMakeOpaque(captureSource));
    }
}
