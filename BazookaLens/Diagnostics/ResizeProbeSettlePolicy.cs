namespace BazookaLens.Diagnostics;

internal static class ResizeProbeSettlePolicy
{
    public static ResizeProbeSettleWaitDecision Decide(
        int elapsedTicks,
        int consecutiveStableTicks,
        int minimumSettleTicks,
        int requiredStableTicks)
    {
        if (elapsedTicks < minimumSettleTicks)
            return new ResizeProbeSettleWaitDecision(false, ResizeProbeSettleWaitReason.WaitingForMinimumTicks);

        if (consecutiveStableTicks < requiredStableTicks)
            return new ResizeProbeSettleWaitDecision(false, ResizeProbeSettleWaitReason.WaitingForStableTicks);

        return new ResizeProbeSettleWaitDecision(true, ResizeProbeSettleWaitReason.Ready);
    }
}

internal readonly record struct ResizeProbeSettleWaitDecision(bool Ready, ResizeProbeSettleWaitReason Reason);

internal enum ResizeProbeSettleWaitReason
{
    WaitingForMinimumTicks,
    WaitingForStableTicks,
    Ready,
    TimedOut,
}
