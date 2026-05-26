using BazookaLens.Capture;
using BazookaLens.UI;
using Dalamud.Game.ClientState.Keys;
using System.Text.Json;

namespace BazookaLens.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void DefaultsMatchGuiSpec()
    {
        var config = new Configuration();

        Assert.Equal(0, config.Version);
        Assert.Equal(2.0, config.Scale);
        Assert.False(config.RegionEnabled);
        Assert.Null(config.Region);
        Assert.Null(config.SaveDirectory);
        Assert.Equal(CaptureImageFormat.Png, config.ImageFormat);
        Assert.Null(config.Shortcut);
        Assert.Equal(GuideMode.RuleOfThirds, config.GuideMode);
        Assert.Equal(3, config.GridRows);
        Assert.Equal(3, config.GridColumns);
    }

    [Fact]
    public void SanitizeNormalizesScaleAndGridValues()
    {
        var config = new Configuration
        {
            Scale = 1.234,
            GridRows = 0,
            GridColumns = 99,
        };

        config.Sanitize();

        Assert.Equal(1.23, config.Scale);
        Assert.Equal(1, config.GridRows);
        Assert.Equal(24, config.GridColumns);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void SanitizeRestoresInvalidScaleToDefault(double scale)
    {
        var config = new Configuration { Scale = scale };

        config.Sanitize();

        Assert.Equal(2.0, config.Scale);
    }

    [Fact]
    public void ConfigurationRoundTripsPersistedGuiFields()
    {
        var config = new Configuration
        {
            Scale = 1.5,
            RegionEnabled = true,
            Region = new(10, 20, 300, 200),
            SaveDirectory = @"D:\shots",
            ImageFormat = CaptureImageFormat.Bmp,
            Shortcut = new KeyboardShortcut([VirtualKey.CONTROL, VirtualKey.F12]),
            GuideMode = GuideMode.Grid,
            GridRows = 4,
            GridColumns = 5,
        };

        var json = JsonSerializer.Serialize(config);
        var roundTrip = JsonSerializer.Deserialize<Configuration>(json)!;

        Assert.Equal(config.Scale, roundTrip.Scale);
        Assert.Equal(config.RegionEnabled, roundTrip.RegionEnabled);
        Assert.Equal(config.Region, roundTrip.Region);
        Assert.Equal(config.SaveDirectory, roundTrip.SaveDirectory);
        Assert.Equal(config.ImageFormat, roundTrip.ImageFormat);
        Assert.NotNull(roundTrip.Shortcut);
        Assert.Equal(config.Shortcut.Keys, roundTrip.Shortcut.Keys);
        Assert.Equal(config.GuideMode, roundTrip.GuideMode);
        Assert.Equal(config.GridRows, roundTrip.GridRows);
        Assert.Equal(config.GridColumns, roundTrip.GridColumns);
    }
}
