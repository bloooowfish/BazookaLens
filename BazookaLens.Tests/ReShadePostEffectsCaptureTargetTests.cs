using BazookaLens.Diagnostics;

namespace BazookaLens.Tests;

public sealed class ReShadePostEffectsCaptureTargetTests
{
    [Fact]
    public void MatchesExpectedPostResizeTextureSize()
    {
        var target = new ReShadePostEffectsCaptureTarget(3840, 2160);

        Assert.True(target.Matches(textureWidth: 3840, textureHeight: 2160));
    }

    [Fact]
    public void RejectsPreviousBackBufferSizeBeforeResizeFinishes()
    {
        var target = new ReShadePostEffectsCaptureTarget(3840, 2160);

        Assert.False(target.Matches(textureWidth: 2560, textureHeight: 1440));
    }

    [Fact]
    public void TimeoutMessageIncludesSkippedFrameDiagnostics()
    {
        var diagnostics = new ReShadePostEffectsCaptureTimeoutDiagnostics();

        diagnostics.RecordTextureSizeMismatch(2560, 1440);
        var message = diagnostics.BuildTimeoutMessage(
            TimeSpan.FromMilliseconds(1),
            new ReShadePostEffectsCaptureTarget(3840, 2160));

        Assert.Contains("skipped 1 post-effects frame", message);
        Assert.Contains("last mismatch=2560x1440", message);
        Assert.Contains("expected=3840x2160", message);
    }
}
