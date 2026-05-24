using BazookaLens.Capture;
using BazookaLens.UI;

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
        var committed = false;
        var service = CreateService(
            uiState,
            (_, token) =>
            {
                captured = true;
                return Task.FromResult(@"C:\shot.png");
            },
            controller,
            () => committed = true);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.CaptureFromConfiguredSettingsAsync(null, requireInteractiveAvailability: true, cts.Token));

        Assert.False(captured);
        Assert.False(committed);
        Assert.False(uiState.IsCapturing);
        Assert.False(controller.IsSuppressed);
    }

    private static CaptureRequestService CreateService(
        CaptureUiState uiState,
        Func<CaptureOptions, CancellationToken, Task<string>> captureAsync,
        OwnUiSuppressionController? controller = null,
        Action? commitCurrentRegion = null)
    {
        var configuration = new Configuration();
        var provider = new CaptureSettingsProvider(configuration);
        return new CaptureRequestService(
            provider,
            captureAsync,
            controller ?? new OwnUiSuppressionController(),
            uiState,
            commitCurrentRegion);
    }
}
