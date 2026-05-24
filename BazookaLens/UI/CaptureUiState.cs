namespace BazookaLens.UI;

internal sealed class CaptureUiState
{
    public bool IsCapturing { get; private set; }

    public bool IsShortcutRecording { get; set; }

    public bool HasInvalidScaleDraft { get; set; }

    public bool IsTextInputActive { get; set; }

    public bool CanStartCapture => !this.IsCapturing;

    public bool CanTriggerInteractiveCapture => this.CanStartCapture
        && !this.IsShortcutRecording
        && !this.HasInvalidScaleDraft
        && !this.IsTextInputActive;

    public IDisposable BeginCapture()
    {
        if (this.IsCapturing)
            throw new InvalidOperationException("A capture is already in progress.");

        this.IsCapturing = true;
        return new CaptureScope(this);
    }

    private sealed class CaptureScope : IDisposable
    {
        private readonly CaptureUiState owner;
        private bool disposed;

        public CaptureScope(CaptureUiState owner)
        {
            this.owner = owner;
        }

        public void Dispose()
        {
            if (this.disposed)
                return;

            this.owner.IsCapturing = false;
            this.disposed = true;
        }
    }
}
