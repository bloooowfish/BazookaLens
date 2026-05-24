using BazookaLens.UI;

namespace BazookaLens.Capture;

internal sealed class CaptureRequestService
{
    private readonly CaptureSettingsProvider settingsProvider;
    private readonly Func<CaptureOptions, CancellationToken, Task<string>> captureAsync;
    private readonly OwnUiSuppressionController ownUiSuppressionController;
    private readonly CaptureUiState captureUiState;
    private readonly Action commitCurrentRegion;

    public CaptureRequestService(
        CaptureSettingsProvider settingsProvider,
        CaptureCoordinator captureCoordinator,
        OwnUiSuppressionController ownUiSuppressionController,
        CaptureUiState captureUiState,
        Action? commitCurrentRegion = null)
        : this(
            settingsProvider,
            captureCoordinator.CaptureAsync,
            ownUiSuppressionController,
            captureUiState,
            commitCurrentRegion)
    {
    }

    internal CaptureRequestService(
        CaptureSettingsProvider settingsProvider,
        Func<CaptureOptions, CancellationToken, Task<string>> captureAsync,
        OwnUiSuppressionController ownUiSuppressionController,
        CaptureUiState captureUiState,
        Action? commitCurrentRegion = null)
    {
        this.settingsProvider = settingsProvider;
        this.captureAsync = captureAsync;
        this.ownUiSuppressionController = ownUiSuppressionController;
        this.captureUiState = captureUiState;
        this.commitCurrentRegion = commitCurrentRegion ?? (() => { });
    }

    public async Task<string> CaptureFromConfiguredSettingsAsync(
        double? scaleOverride,
        bool requireInteractiveAvailability,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (requireInteractiveAvailability)
        {
            if (!this.captureUiState.CanTriggerInteractiveCapture)
                throw new InvalidOperationException("Capture is not available while Bazooka Lens is editing input.");
        }
        else if (!this.captureUiState.CanStartCapture)
        {
            throw new InvalidOperationException("A capture is already in progress.");
        }

        using var captureScope = this.captureUiState.BeginCapture();
        this.commitCurrentRegion();
        var options = this.settingsProvider.CreateShootOptions(scaleOverride);

        using (this.ownUiSuppressionController.Suppress())
        {
            return await this.captureAsync(options, cancellationToken).ConfigureAwait(false);
        }
    }
}
