using System.Text;

namespace BazookaLens.Diagnostics;

internal static class ReShadeEventBridgeLogPolicy
{
    public static bool ShouldLogModuleResolverHit(long hitCount)
    {
        return ShouldLogHighFrequencyEvent(hitCount);
    }

    public static bool ShouldLogHighFrequencyEvent(long eventCount)
    {
        return eventCount <= 5 || eventCount % 600 == 0;
    }

    public static bool ShouldLogLowFrequencyEvent(long eventCount)
    {
        return eventCount <= 5 || eventCount % 120 == 0;
    }

    public static bool ShouldLogSkippedPostEffectsCapture(long skippedCount)
    {
        return skippedCount <= 5 || skippedCount % 120 == 0;
    }
}

internal static class ReShadeEventBridgeModuleResolver
{
    public static bool ShouldResolve(nint requestedAddress, nint addonModuleHandle, IReadOnlyCollection<nint> callbackPointers)
    {
        return addonModuleHandle != nint.Zero &&
            (requestedAddress == addonModuleHandle || callbackPointers.Contains(requestedAddress));
    }
}

internal static class ReShadeEventBridgeStatusFormatter
{
    public static string AppendStoppedFooter(string preStopSummary, bool activeAfterStop)
    {
        var sb = new StringBuilder(preStopSummary.TrimEnd());
        sb.AppendLine();
        sb.AppendLine("Stopped=True");
        sb.Append($"ActiveAfterStop={activeAfterStop}");
        return sb.ToString();
    }
}
