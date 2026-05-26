namespace BazookaLens.Capture;

internal sealed record CaptureSettings(
    double Scale,
    bool RegionEnabled,
    CaptureRegion? Region,
    string? SaveDirectory,
    CaptureImageFormat ImageFormat);
