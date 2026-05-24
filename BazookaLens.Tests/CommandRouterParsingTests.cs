using BazookaLens.Capture;
using BazookaLens.Commands;

namespace BazookaLens.Tests;

public sealed class CommandRouterParsingTests
{
    [Fact]
    public void ParseRecognizesHelp()
    {
        var parsed = CommandRouter.Parse("help");

        Assert.Equal(BlensCommand.Help, parsed.Command);
        Assert.Null(parsed.CaptureOptions);
    }

    [Fact]
    public void HelpTextDocumentsDefaultShootWorkflow()
    {
        Assert.Contains("/blens shoot [scale]", CommandRouter.HelpText);
        Assert.DoesNotContain("/native" + "shot", CommandRouter.HelpText);
        Assert.Contains("configured scale unless overridden", CommandRouter.HelpText);
        Assert.DoesNotContain("scale=2", CommandRouter.HelpText);
        Assert.DoesNotContain("default 2", CommandRouter.HelpText);
        Assert.Contains("hide-ui", CommandRouter.HelpText);
        Assert.Contains("open-folder", CommandRouter.HelpText);
    }

    [Fact]
    public void ParseRecognizesStatus()
    {
        var parsed = CommandRouter.Parse("status");

        Assert.Equal(BlensCommand.Status, parsed.Command);
    }

    [Fact]
    public void ParseRecognizesReShadeStatus()
    {
        var parsed = CommandRouter.Parse("reshade-status");

        Assert.Equal(BlensCommand.ReShadeStatus, parsed.Command);
    }

    [Theory]
    [InlineData("reshade-events start", "Start")]
    [InlineData("reshade-events stop", "Stop")]
    [InlineData("reshade-events status", "Status")]
    public void ParseRecognizesReShadeEventsActions(string args, string expectedActionName)
    {
        var parsed = CommandRouter.Parse(args);

        Assert.Equal(BlensCommand.ReShadeEvents, parsed.Command);
        var expectedAction = Enum.Parse<ReShadeEventsAction>(expectedActionName);
        Assert.Equal(expectedAction, parsed.ReShadeEventsAction);
    }

    [Theory]
    [InlineData("resize-probe 2", 2.0, "DryRun")]
    [InlineData("resize-probe 1.5 dry-run", 1.5, "DryRun")]
    [InlineData("resize-probe 1.25 device", 1.25, "Device")]
    public void ParseRecognizesResizeProbe(string args, double expectedScale, string expectedRouteName)
    {
        var parsed = CommandRouter.Parse(args);

        Assert.Equal(BlensCommand.ResizeProbe, parsed.Command);
        Assert.NotNull(parsed.ResizeProbeOptions);
        Assert.Equal(expectedScale, parsed.ResizeProbeOptions.Scale);
        var expectedRoute = Enum.Parse<ResizeProbeRoute>(expectedRouteName);
        Assert.Equal(expectedRoute, parsed.ResizeProbeOptions.Route);
    }

    [Fact]
    public void ParseRecognizesRestoreUi()
    {
        var parsed = CommandRouter.Parse("restore-ui");

        Assert.Equal(BlensCommand.RestoreUi, parsed.Command);
    }

    [Fact]
    public void ParseRecognizesRestoreDisplay()
    {
        var parsed = CommandRouter.Parse("restore-display");

        Assert.Equal(BlensCommand.RestoreDisplay, parsed.Command);
        Assert.NotNull(parsed.RestoreDisplayOptions);
        Assert.False(parsed.RestoreDisplayOptions.ForceWindowRefresh);
    }

    [Fact]
    public void ParseRecognizesRestoreDisplayForce()
    {
        var parsed = CommandRouter.Parse("restore-display force");

        Assert.Equal(BlensCommand.RestoreDisplay, parsed.Command);
        Assert.NotNull(parsed.RestoreDisplayOptions);
        Assert.True(parsed.RestoreDisplayOptions.ForceWindowRefresh);
    }

    [Fact]
    public void ParseDefaultsCaptureToAfterImGuiWithUiVisible()
    {
        var parsed = CommandRouter.Parse("capture");

        Assert.Equal(BlensCommand.Capture, parsed.Command);
        Assert.NotNull(parsed.CaptureOptions);
        Assert.Equal(CaptureTiming.AfterImGui, parsed.CaptureOptions.Timing);
        Assert.False(parsed.CaptureOptions.HideGameUi);
        Assert.Null(parsed.CaptureOptions.Region);
        Assert.Equal(1.0, parsed.CaptureOptions.Scale);
    }

    [Fact]
    public void ParseShootReturnsSettingsBackedShootCommand()
    {
        var parsed = CommandRouter.Parse("shoot");

        Assert.Equal(BlensCommand.Shoot, parsed.Command);
        Assert.Null(parsed.CaptureOptions);
        Assert.Null(parsed.ShootScaleOverride);
    }

    [Fact]
    public void ParseShootAcceptsOptionalScaleOverride()
    {
        var parsed = CommandRouter.Parse("shoot 1.5");

        Assert.Equal(BlensCommand.Shoot, parsed.Command);
        Assert.Null(parsed.CaptureOptions);
        Assert.Equal(1.5, parsed.ShootScaleOverride);
    }

    [Fact]
    public void ParseRecognizesOpenFolder()
    {
        var parsed = CommandRouter.Parse("open-folder");

        Assert.Equal(BlensCommand.OpenFolder, parsed.Command);
    }

    [Fact]
    public void ParseCaptureSupportsBeforeAndHideUi()
    {
        var parsed = CommandRouter.Parse("capture before hide-ui");

        Assert.Equal(CaptureTiming.BeforeImGui, parsed.CaptureOptions!.Timing);
        Assert.True(parsed.CaptureOptions.HideGameUi);
    }

    [Fact]
    public void ParseCaptureRegionReadsCoordinatesTimingAndUiFlag()
    {
        var parsed = CommandRouter.Parse("capture-region 10 20 300 400 after hide-ui");

        Assert.Equal(BlensCommand.Capture, parsed.Command);
        Assert.NotNull(parsed.CaptureOptions);
        Assert.Equal(CaptureTiming.AfterImGui, parsed.CaptureOptions.Timing);
        Assert.True(parsed.CaptureOptions.HideGameUi);
        Assert.Equal(new CaptureRegion(10, 20, 300, 400), parsed.CaptureOptions.Region);
        Assert.Equal(1.0, parsed.CaptureOptions.Scale);
    }

    [Fact]
    public void ParseCaptureScaleReadsScaleTimingAndUiFlag()
    {
        var parsed = CommandRouter.Parse("capture-scale 2 before hide-ui");

        Assert.Equal(BlensCommand.Capture, parsed.Command);
        Assert.NotNull(parsed.CaptureOptions);
        Assert.Equal(CaptureTiming.BeforeImGui, parsed.CaptureOptions.Timing);
        Assert.True(parsed.CaptureOptions.HideGameUi);
        Assert.Null(parsed.CaptureOptions.Region);
        Assert.Equal(2.0, parsed.CaptureOptions.Scale);
    }

    [Fact]
    public void ParseCaptureRegionScaleReadsCoordinatesScaleTimingAndUiFlag()
    {
        var parsed = CommandRouter.Parse("capture-region-scale 10 20 300 400 1.5 after hide-ui");

        Assert.Equal(BlensCommand.Capture, parsed.Command);
        Assert.NotNull(parsed.CaptureOptions);
        Assert.Equal(CaptureTiming.AfterImGui, parsed.CaptureOptions.Timing);
        Assert.True(parsed.CaptureOptions.HideGameUi);
        Assert.Equal(new CaptureRegion(10, 20, 300, 400), parsed.CaptureOptions.Region);
        Assert.Equal(1.5, parsed.CaptureOptions.Scale);
    }

    [Theory]
    [InlineData("capture nonsense")]
    [InlineData("capture before after")]
    [InlineData("capture-region 10 20 30")]
    [InlineData("capture-region x 20 30 40")]
    [InlineData("capture-region 10 20 30 40 unexpected")]
    [InlineData("capture-scale")]
    [InlineData("capture-scale nope")]
    [InlineData("capture-scale 0")]
    [InlineData("capture-scale -1")]
    [InlineData("capture-scale 4.1")]
    [InlineData("capture-scale 2 before after")]
    [InlineData("capture-region-scale 10 20 30 40")]
    [InlineData("capture-region-scale 10 20 30 40 nope")]
    [InlineData("capture-region-scale 10 20 30 40 0")]
    [InlineData("capture-region-scale 10 20 30 40 4.1")]
    [InlineData("capture-region-scale 10 20 30 40 2 unexpected")]
    [InlineData("shoot nope")]
    [InlineData("shoot 0")]
    [InlineData("shoot 4.1")]
    [InlineData("shoot 2 extra")]
    [InlineData("open-folder extra")]
    [InlineData("reshade-events")]
    [InlineData("reshade-events start stop")]
    [InlineData("reshade-events reload")]
    [InlineData("resize-probe")]
    [InlineData("resize-probe nope")]
    [InlineData("resize-probe 0")]
    [InlineData("resize-probe -1")]
    [InlineData("resize-probe 4.1")]
    [InlineData("resize-probe 2 unknown")]
    [InlineData("resize-probe 2 windowed")]
    [InlineData("resize-probe 2 device extra")]
    [InlineData("restore-display force extra")]
    [InlineData("restore-display unknown")]
    [InlineData("unknown")]
    public void ParseRejectsInvalidCommands(string args)
    {
        Assert.Throws<ArgumentException>(() => CommandRouter.Parse(args));
    }
}
