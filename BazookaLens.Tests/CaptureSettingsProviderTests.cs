using BazookaLens.Capture;

namespace BazookaLens.Tests;

public sealed class CaptureSettingsProviderTests
{
    [Fact]
    public void BuildsDefaultShootOptionsFromConfiguration()
    {
        var config = new Configuration();
        var provider = new CaptureSettingsProvider(config);

        var options = provider.CreateShootOptions(scaleOverride: null);

        Assert.Equal(CaptureTiming.AfterImGui, options.Timing);
        Assert.True(options.HideGameUi);
        Assert.Null(options.Region);
        Assert.Equal(2.0, options.Scale);
    }

    [Fact]
    public void UsesStoredRegionWhenEnabled()
    {
        var config = new Configuration
        {
            RegionEnabled = true,
            Region = new CaptureRegion(100, 100, 400, 300),
        };
        var provider = new CaptureSettingsProvider(config);

        var options = provider.CreateShootOptions(scaleOverride: null);

        Assert.Equal(new CaptureRegion(100, 100, 400, 300), options.Region);
    }

    [Fact]
    public void FullFramePreservesStoredRegionButDoesNotUseIt()
    {
        var config = new Configuration
        {
            RegionEnabled = false,
            Region = new CaptureRegion(100, 100, 400, 300),
        };
        var provider = new CaptureSettingsProvider(config);

        var options = provider.CreateShootOptions(scaleOverride: null);

        Assert.Null(options.Region);
        Assert.Equal(new CaptureRegion(100, 100, 400, 300), config.Region);
    }

    [Fact]
    public void ScaleOverrideDoesNotPersistToConfiguration()
    {
        var config = new Configuration { Scale = 2.0 };
        var provider = new CaptureSettingsProvider(config);

        var options = provider.CreateShootOptions(scaleOverride: 1.5);

        Assert.Equal(1.5, options.Scale);
        Assert.Equal(2.0, config.Scale);
    }

    [Fact]
    public void SettingsSnapshotIncludesImageFormat()
    {
        var config = new Configuration { ImageFormat = CaptureImageFormat.Bmp };
        var provider = new CaptureSettingsProvider(config);

        var settings = provider.CreateSettings();

        Assert.Equal(CaptureImageFormat.Bmp, settings.ImageFormat);
    }
}
