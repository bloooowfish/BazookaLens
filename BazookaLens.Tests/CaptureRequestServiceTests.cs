using BazookaLens.Capture;
using BazookaLens.UI;
using Dalamud.Interface.Windowing;

namespace BazookaLens.Tests;

public sealed class CaptureRequestServiceTests
{
    [Fact]
    public async Task InteractiveCaptureIsBlockedByInvalidScaleDraft()
    {
        var uiState = new CaptureUiState { HasInvalidScaleDraft = true };
        var service = CreateService(uiState, (_, _) => Task.FromResult("unused"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CaptureFromConfiguredSettingsAsync(null, requireInteractiveAvailability: true, CancellationToken.None));
    }

    [Fact]
    public async Task CommandCaptureIgnoresInteractiveEditState()
    {
        var captured = false;
        var uiState = new CaptureUiState { HasInvalidScaleDraft = true, IsTextInputActive = true };
        var service = CreateService(
            uiState,
            (options, _) =>
            {
                captured = true;
                Assert.Equal(2.0, options.Scale);
                return Task.FromResult(@"C:\shot.png");
            });

        var output = await service.CaptureFromConfiguredSettingsAsync(null, requireInteractiveAvailability: false, CancellationToken.None);

        Assert.True(captured);
        Assert.Equal(@"C:\shot.png", output);
    }

    [Fact]
    public async Task CaptureScopeAndOwnUiSuppressionRestoreAfterCaptureFailure()
    {
        var controller = new OwnUiSuppressionController();
        var uiState = new CaptureUiState();
        var service = CreateService(
            uiState,
            (_, _) =>
            {
                Assert.True(controller.IsSuppressed);
                Assert.True(uiState.IsCapturing);
                throw new InvalidOperationException("boom");
            },
            controller);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CaptureFromConfiguredSettingsAsync(null, requireInteractiveAvailability: false, CancellationToken.None));

        Assert.False(controller.IsSuppressed);
        Assert.False(uiState.IsCapturing);
    }

    [Fact]
    public async Task CanceledCaptureDoesNotMutateCaptureOrUiState()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var controller = new OwnUiSuppressionController();
        var uiState = new CaptureUiState();
        var captured = false;
        var closed = false;
        var service = CreateService(
            uiState,
            (_, token) =>
            {
                captured = true;
                return Task.FromResult(@"C:\shot.png");
            },
            controller,
            () => closed = true);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.CaptureFromConfiguredSettingsAsync(null, requireInteractiveAvailability: true, cts.Token));

        Assert.False(captured);
        Assert.False(closed);
        Assert.False(uiState.IsCapturing);
        Assert.False(controller.IsSuppressed);
    }

    [Fact]
    public async Task CaptureClosesRegionEditorBeforeSuppressionSnapshot()
    {
        var controller = new OwnUiSuppressionController();
        var overlayWindow = new TestWindow { IsOpen = true };
        controller.Register(overlayWindow);
        var uiState = new CaptureUiState();
        var service = CreateService(
            uiState,
            (_, _) =>
            {
                Assert.True(controller.IsSuppressed);
                Assert.False(overlayWindow.IsOpen);
                return Task.FromResult(@"C:\shot.png");
            },
            controller,
            closeRegionEditorForCapture: () => overlayWindow.IsOpen = false);

        await service.CaptureFromConfiguredSettingsAsync(null, requireInteractiveAvailability: true, CancellationToken.None);

        Assert.False(overlayWindow.IsOpen);
    }

    private static CaptureRequestService CreateService(
        CaptureUiState uiState,
        Func<CaptureOptions, CancellationToken, Task<string>> captureAsync,
        OwnUiSuppressionController? controller = null,
        Action? closeRegionEditorForCapture = null)
    {
        var configuration = new Configuration();
        var provider = new CaptureSettingsProvider(configuration);
        return new CaptureRequestService(
            provider,
            captureAsync,
            controller ?? new OwnUiSuppressionController(),
            uiState,
            closeRegionEditorForCapture);
    }

    private sealed class TestWindow : Window
    {
        public TestWindow()
            : base("Test###CaptureRequestServiceTests")
        {
        }

        public override void Draw()
        {
        }
    }
}
