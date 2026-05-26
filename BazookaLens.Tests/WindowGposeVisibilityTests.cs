using BazookaLens.Capture;
using BazookaLens.UI;
using BazookaLens.Windows;

namespace BazookaLens.Tests;

public sealed class WindowGposeVisibilityTests
{
    [Fact]
    public void MainWindowIsForcedIntoMainGameWindowForGpose()
    {
        var configuration = new Configuration { SaveDirectory = Path.GetTempPath() };
        var suppressionController = new OwnUiSuppressionController();
        var regionOverlay = new RegionOverlayWindow(configuration, suppressionController);
        var shortcutOverlay = new ShortcutCaptureOverlayWindow(configuration, suppressionController, new CaptureUiState());
        var mainWindow = new BazookaLensWindow(
            configuration,
            () => Task.CompletedTask,
            new CaptureUiState(),
            new CapturePathService(() => configuration.SaveDirectory),
            regionOverlay,
            shortcutOverlay);

        Assert.True(mainWindow.ForceMainWindow);
    }

    [Fact]
    public void OverlayWindowsAreForcedIntoMainGameWindowForGpose()
    {
        var configuration = new Configuration();
        var suppressionController = new OwnUiSuppressionController();

        Assert.True(new RegionOverlayWindow(configuration, suppressionController).ForceMainWindow);
        Assert.True(new ShortcutCaptureOverlayWindow(configuration, suppressionController, new CaptureUiState()).ForceMainWindow);
    }
}
