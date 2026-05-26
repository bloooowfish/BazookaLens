using BazookaLens.Capture;

namespace BazookaLens.Tests;

public sealed class TextureCaptureSavePolicyTests
{
    [Theory]
    [InlineData("ReShadeFinishEffects", CaptureImageFormat.Png, true)]
    [InlineData("ReShadeFinishEffects", CaptureImageFormat.Bmp, false)]
    [InlineData("Viewport", CaptureImageFormat.Png, false)]
    [InlineData("Other", CaptureImageFormat.Png, false)]
    internal void MakesOnlyPngReShadeFinishEffectsCapturesOpaque(
        string captureSource,
        CaptureImageFormat format,
        bool expected)
    {
        Assert.Equal(expected, TextureCaptureSavePolicy.ShouldMakeOpaque(captureSource, format));
    }
}
