namespace BazookaLens.Capture;

internal sealed record CaptureOptions(
    CaptureTiming Timing,
    bool HideGameUi,
    CaptureRegion? Region,
    double Scale = 1.0);
