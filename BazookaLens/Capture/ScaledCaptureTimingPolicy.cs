namespace BazookaLens.Capture;

internal static class ScaledCaptureTimingPolicy
{
    public const int PresentationSettleTicks = 120;
    public const int PresentationStableTicks = 5;
    public const int PostRestoreReShadeStabilizationMaxTicks = 300;
}
