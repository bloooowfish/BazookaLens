using System;
using System.Threading;
using System.Threading.Tasks;

using BazookaLens.Diagnostics;
using BazookaLens.UI;

namespace BazookaLens.Capture;

internal sealed class CaptureCoordinator
{
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private readonly object uiTransactionLock = new();
    private readonly ViewportCaptureService viewportCaptureService;
    private readonly UiVisibilityController uiVisibilityController;
    private readonly ResizeProbeService resizeProbeService;
    private readonly ReShadeEventBridge reShadeEventBridge;
    private long operationSequence;
    private bool unloadRequested;

    public CaptureCoordinator(
        ViewportCaptureService viewportCaptureService,
        UiVisibilityController uiVisibilityController,
        ResizeProbeService resizeProbeService,
        ReShadeEventBridge reShadeEventBridge)
    {
        this.viewportCaptureService = viewportCaptureService;
        this.uiVisibilityController = uiVisibilityController;
        this.resizeProbeService = resizeProbeService;
        this.reShadeEventBridge = reShadeEventBridge;
    }

    public async Task<string> CaptureAsync(CaptureOptions options, CancellationToken cancellationToken = default)
    {
        var operationId = Interlocked.Increment(ref this.operationSequence);
        var uiHidden = false;
        PluginServices.Log.Information("Capture operation {OperationId} waiting for operation gate.", operationId);
        await this.operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            this.ThrowIfUnloadRequested(cancellationToken);
            PluginServices.Log.Information("Capture operation {OperationId} started: {Options}", operationId, options);
            options = options with { Scale = CaptureScalePolicy.NormalizeOrThrow(options.Scale) };

            if (RequiresScaledCapture(options))
                return await this.CaptureScaledAsync(operationId, options, cancellationToken).ConfigureAwait(false);

            await this.viewportCaptureService.ValidateOptionsAsync(options, cancellationToken).ConfigureAwait(false);
            this.TryAutoStartReShadeEventBridge(operationId, options);

            if (options.HideGameUi)
                uiHidden = await this.BeginHideGameUiAsync(operationId, cancellationToken).ConfigureAwait(false);

            var postEffectsOutput = await this.TryCapturePostEffectsWithoutResizeAsync(operationId, options, cancellationToken).ConfigureAwait(false);
            if (postEffectsOutput is not null)
                return postEffectsOutput;

            var output = await this.viewportCaptureService.CaptureAsync(options, cancellationToken).ConfigureAwait(false);
            PluginServices.Log.Information("Capture operation {OperationId} completed: {OutputPath}", operationId, output);
            return output;
        }
        finally
        {
            if (uiHidden)
                await this.RestoreUiBestEffortAsync(operationId).ConfigureAwait(false);

            this.operationGate.Release();
            PluginServices.Log.Information("Capture operation {OperationId} released operation gate.", operationId);
        }
    }

    private async Task<string> CaptureScaledAsync(long operationId, CaptureOptions options, CancellationToken cancellationToken)
    {
        return await this.resizeProbeService.RunExclusiveAsync(
            async () =>
            {
                var uiHidden = false;
                ResizeProbeTarget? source = null;
                ResizeProbeTarget? target = null;
                ResizeProbeService.ResizeProbeWaitResult? targetWait = null;
                ResizeProbeService.ResizeProbeSettleWaitResult? settleWait = null;
                ResizeProbeService.ResizeProbeWaitResult? restoreWait = null;
                ReShadePostEffectsCaptureRequest? postEffectsCaptureRequest = null;
                var resized = false;
                var usedPostEffectsCapture = false;

                try
                {
                    var before = await PluginServices.Framework
                        .RunOnFrameworkThread(ResizeProbeRenderState.Capture)
                        .ConfigureAwait(false);
                    source = before.RequireDeviceTarget();
                    target = ResizeProbeTarget.FromScale(source.Width, source.Height, options.Scale);
                    ResizeProbeService.ValidateMutableRouteTarget(target);

                    var scaledOptions = CreateScaledCaptureOptions(options);
                    ValidateCaptureRegionAgainstTarget(scaledOptions, target);

                    PluginServices.Log.Information(
                        "Capture operation {OperationId} scaled capture target prepared: Scale={Scale}, Source={SourceWidth}x{SourceHeight}, Target={TargetWidth}x{TargetHeight}, SourceRegion={SourceRegion}, TargetRegion={TargetRegion}",
                        operationId,
                        options.Scale,
                        source.Width,
                        source.Height,
                        target.Width,
                        target.Height,
                        options.Region?.ToString() ?? "<full>",
                        scaledOptions.Region?.ToString() ?? "<full>");

                    this.TryAutoStartReShadeEventBridge(operationId, options);

                    if (options.HideGameUi)
                        uiHidden = await this.BeginHideGameUiAsync(operationId, cancellationToken).ConfigureAwait(false);

                    var observedReShadeCounts = this.reShadeEventBridge.GetEventCounts();

                    await PluginServices.Framework
                        .RunOnFrameworkThread(() => ResizeProbeService.ApplyDeviceTarget(target))
                        .ConfigureAwait(false);
                    resized = true;

                    targetWait = await ResizeProbeService.WaitForPresentationTargetAsync(target, cancellationToken).ConfigureAwait(false);
                    if (!targetWait.Reached)
                    {
                        throw new InvalidOperationException(
                            $"Scaled capture presentation target {target.Width}x{target.Height} was not reached within {ResizeProbeService.MaxWaitTicks} ticks. LastState={targetWait.State.ToSummary()}");
                    }

                    settleWait = await ResizeProbeService
                        .WaitForPresentationTargetSettleAsync(
                            target,
                            ScaledCaptureTimingPolicy.PresentationSettleTicks,
                            ScaledCaptureTimingPolicy.PresentationStableTicks,
                            cancellationToken)
                        .ConfigureAwait(false);
                    var afterSettleReShadeCounts = this.reShadeEventBridge.GetEventCounts();
                    if (!settleWait.Ready)
                    {
                        throw new InvalidOperationException(
                            $"Scaled capture presentation target {target.Width}x{target.Height} did not settle within {settleWait.Ticks} ticks. Reason={settleWait.Reason}, LastState={settleWait.State.ToSummary()}");
                    }

                    PluginServices.Log.Information(
                        "Capture operation {OperationId} scaled capture target settled: SettleTicks={SettleTicks}, StableTicks={StableTicks}, ReShadeBefore={ReShadeBefore}, ReShadeAfter={ReShadeAfter}",
                        operationId,
                        settleWait.Ticks,
                        settleWait.ConsecutiveStableTicks,
                        observedReShadeCounts,
                        afterSettleReShadeCounts);

                    await this.viewportCaptureService.ValidateOptionsAsync(scaledOptions, cancellationToken).ConfigureAwait(false);
                    if (ReShadePostEffectsCapturePolicy.ShouldArmPostEffectsCaptureAfterSettle(
                             options.Timing,
                             this.reShadeEventBridge.IsActive,
                             observedReShadeCounts,
                             afterSettleReShadeCounts))
                    {
                        postEffectsCaptureRequest = this.reShadeEventBridge.ArmNextPostEffectsCapture(
                            scaledOptions.Region,
                            new ReShadePostEffectsCaptureTarget(target.Width, target.Height));

                        using var postEffectsTexture = await this.reShadeEventBridge
                            .WaitForPostEffectsCaptureAsync(postEffectsCaptureRequest, ReShadePostEffectsCapturePolicy.CaptureTimeout, cancellationToken)
                            .ConfigureAwait(false);
                        postEffectsCaptureRequest = null;
                        var postEffectsOutput = await this.viewportCaptureService
                            .SaveTextureAsync(postEffectsTexture, scaledOptions, TextureCaptureSavePolicy.ReShadeFinishEffectsSource, cancellationToken)
                            .ConfigureAwait(false);
                        usedPostEffectsCapture = true;

                        PluginServices.Log.Information(
                            "Capture operation {OperationId} scaled post-ReShade capture completed: OutputPath={OutputPath}, TargetReachedTicks={TargetWaitTicks}, SettleTicks={SettleTicks}",
                            operationId,
                            postEffectsOutput,
                            targetWait.Ticks,
                            settleWait.Ticks);
                        return postEffectsOutput;
                    }

                    if (options.Timing == CaptureTiming.AfterImGui)
                    {
                        PluginServices.Log.Information(
                            "Capture operation {OperationId} using viewport capture path because ReShade post-effects capture is unavailable: BridgeActive={BridgeActive}, PostEffectsRequestArmed={PostEffectsRequestArmed}, FinishEffectsBefore={FinishEffectsBefore}, FinishEffectsAfter={FinishEffectsAfter}",
                            operationId,
                            this.reShadeEventBridge.IsActive,
                            postEffectsCaptureRequest is not null,
                            observedReShadeCounts.FinishEffects,
                            afterSettleReShadeCounts.FinishEffects);
                    }

                    var output = await this.viewportCaptureService.CaptureAsync(scaledOptions, cancellationToken).ConfigureAwait(false);
                    PluginServices.Log.Information(
                        "Capture operation {OperationId} scaled capture completed: OutputPath={OutputPath}, TargetReachedTicks={TargetWaitTicks}, SettleTicks={SettleTicks}",
                        operationId,
                        output,
                        targetWait.Ticks,
                        settleWait.Ticks);
                    return output;
                }
                finally
                {
                    if (postEffectsCaptureRequest is not null)
                    {
                        this.reShadeEventBridge.CancelPostEffectsCapture(postEffectsCaptureRequest);
                        postEffectsCaptureRequest = null;
                    }

                    if (resized && source is not null)
                    {
                        try
                        {
                            var beforeRestoreReShadeSnapshot = this.reShadeEventBridge.GetEventSnapshot();
                            await PluginServices.Framework
                                .RunOnFrameworkThread(() => ResizeProbeService.ApplyDeviceTarget(source))
                                .ConfigureAwait(false);
                            restoreWait = await ResizeProbeService.WaitForPresentationTargetAsync(source, CancellationToken.None).ConfigureAwait(false);
                            PluginServices.Log.Information(
                                "Capture operation {OperationId} scaled capture restore completed: RestoreReached={RestoreReached}, RestoreWaitTicks={RestoreWaitTicks}",
                                operationId,
                                restoreWait.Reached,
                                restoreWait.Ticks);

                            if (ShouldWaitForReShadeRestoreStabilization(
                                    usedPostEffectsCapture,
                                    this.reShadeEventBridge.IsActive,
                                    cancellationToken))
                            {
                                var reShadeRestoreWait = await WaitForReShadeRestoreStabilizationAsync(
                                        this.reShadeEventBridge,
                                        beforeRestoreReShadeSnapshot,
                                        CancellationToken.None)
                                    .ConfigureAwait(false);
                                if (reShadeRestoreWait.Ready)
                                {
                                    PluginServices.Log.Information(
                                        "Capture operation {OperationId} ReShade restore stabilization completed: Ready={Ready}, Ticks={Ticks}, Reason={Reason}, Snapshot={Snapshot}",
                                        operationId,
                                        reShadeRestoreWait.Ready,
                                        reShadeRestoreWait.Ticks,
                                        reShadeRestoreWait.Reason,
                                        reShadeRestoreWait.Snapshot);
                                }
                                else
                                {
                                    PluginServices.Log.Warning(
                                        "Capture operation {OperationId} ReShade restore stabilization completed: Ready={Ready}, Ticks={Ticks}, Reason={Reason}, Snapshot={Snapshot}",
                                        operationId,
                                        reShadeRestoreWait.Ready,
                                        reShadeRestoreWait.Ticks,
                                        reShadeRestoreWait.Reason,
                                        reShadeRestoreWait.Snapshot);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            PluginServices.Log.Warning(ex, "Capture operation {OperationId} failed to restore device resolution after scaled capture.", operationId);
                        }
                    }

                    if (uiHidden)
                        await this.RestoreUiBestEffortAsync(operationId).ConfigureAwait(false);
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> TryCapturePostEffectsWithoutResizeAsync(
        long operationId,
        CaptureOptions options,
        CancellationToken cancellationToken)
    {
        if (!ReShadePostEffectsCapturePolicy.ShouldTryPostEffectsCaptureWithoutResize(options.Timing, this.reShadeEventBridge.IsActive))
            return null;

        ReShadePostEffectsCaptureRequest? postEffectsCaptureRequest = null;
        try
        {
            var renderState = await PluginServices.Framework
                .RunOnFrameworkThread(ResizeProbeRenderState.Capture)
                .ConfigureAwait(false);
            var target = renderState.RequireDeviceTarget();
            ValidateCaptureRegionAgainstTarget(options, target);

            postEffectsCaptureRequest = this.reShadeEventBridge.ArmNextPostEffectsCapture(
                options.Region,
                new ReShadePostEffectsCaptureTarget(target.Width, target.Height));

            using var postEffectsTexture = await this.reShadeEventBridge
                .WaitForPostEffectsCaptureAsync(postEffectsCaptureRequest, ReShadePostEffectsCapturePolicy.CaptureTimeout, cancellationToken)
                .ConfigureAwait(false);
            postEffectsCaptureRequest = null;

            var output = await this.viewportCaptureService
                .SaveTextureAsync(postEffectsTexture, options, TextureCaptureSavePolicy.ReShadeFinishEffectsSource, cancellationToken)
                .ConfigureAwait(false);

            PluginServices.Log.Information(
                "Capture operation {OperationId} post-ReShade capture completed without resize: OutputPath={OutputPath}, Target={TargetWidth}x{TargetHeight}",
                operationId,
                output,
                target.Width,
                target.Height);
            return output;
        }
        finally
        {
            if (postEffectsCaptureRequest is not null)
                this.reShadeEventBridge.CancelPostEffectsCapture(postEffectsCaptureRequest);
        }
    }

    private static bool RequiresScaledCapture(CaptureOptions options)
    {
        return Math.Abs(options.Scale - 1.0) > double.Epsilon;
    }

    private static CaptureOptions CreateScaledCaptureOptions(CaptureOptions options)
    {
        var scaledRegion = options.Region?.Scale(options.Scale);
        return options with
        {
            Region = scaledRegion,
            Scale = 1.0,
        };
    }

    private static void ValidateCaptureRegionAgainstTarget(CaptureOptions options, ResizeProbeTarget target)
    {
        if (target.Width > int.MaxValue || target.Height > int.MaxValue)
            throw new InvalidOperationException($"Scaled capture target {target.Width}x{target.Height} exceeds supported viewport dimensions.");

        _ = options.Region?.ToUv((int)target.Width, (int)target.Height);
    }

    private static async Task<ReShadeRestoreStabilizationWaitResult> WaitForReShadeRestoreStabilizationAsync(
        ReShadeEventBridge reShadeEventBridge,
        ReShadeEventSnapshot beforeRestore,
        CancellationToken cancellationToken)
    {
        var current = reShadeEventBridge.GetEventSnapshot();
        var decision = ReShadeRestoreStabilizationPolicy.Decide(beforeRestore, current);
        if (decision.Ready)
            return new ReShadeRestoreStabilizationWaitResult(true, 0, decision.Reason, current);

        for (var tick = 1; tick <= ScaledCaptureTimingPolicy.PostRestoreReShadeStabilizationMaxTicks; tick++)
        {
            await PluginServices.Framework.DelayTicks(1, cancellationToken).ConfigureAwait(false);

            current = reShadeEventBridge.GetEventSnapshot();
            decision = ReShadeRestoreStabilizationPolicy.Decide(beforeRestore, current);
            if (decision.Ready)
                return new ReShadeRestoreStabilizationWaitResult(true, tick, decision.Reason, current);
        }

        return new ReShadeRestoreStabilizationWaitResult(
            false,
            ScaledCaptureTimingPolicy.PostRestoreReShadeStabilizationMaxTicks,
            decision.Reason,
            current);
    }

    private static bool ShouldWaitForReShadeRestoreStabilization(
        bool usedPostEffectsCapture,
        bool reShadeBridgeActive,
        CancellationToken operationToken)
    {
        return usedPostEffectsCapture &&
            reShadeBridgeActive &&
            !operationToken.IsCancellationRequested &&
            !PluginServices.Framework.IsFrameworkUnloading;
    }

    private void TryAutoStartReShadeEventBridge(long operationId, CaptureOptions options)
    {
        if (!ReShadeBridgeAutoStartPolicy.ShouldAutoStart(options.Timing, options.Scale, this.reShadeEventBridge.IsActive))
            return;

        try
        {
            PluginServices.Log.Information("Capture operation {OperationId} auto-starting ReShade event bridge for post-effects capture.", operationId);
            var status = this.reShadeEventBridge.Start();
            PluginServices.Log.Information("Capture operation {OperationId} ReShade event bridge auto-start result:\n{Status}", operationId, status);
        }
        catch (Exception ex)
        {
            PluginServices.Log.Warning(
                ex,
                "Capture operation {OperationId} could not auto-start ReShade event bridge; capture will continue with viewport fallback if post-effects events remain unavailable.",
                operationId);
        }
    }

    public void RequestUnload()
    {
        lock (this.uiTransactionLock)
        {
            this.unloadRequested = true;
        }

        PluginServices.Log.Information("Capture coordinator marked unload requested; new UI hide transactions are blocked.");
    }

    public async Task RestoreUiAsync(CancellationToken cancellationToken = default)
    {
        PluginServices.Log.Information("Manual restore-ui waiting for operation gate.");
        await this.operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            PluginServices.Log.Information("Manual restore-ui acquired operation gate.");
            await this.RestoreUiOnFrameworkThreadAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.operationGate.Release();
            PluginServices.Log.Information("Manual restore-ui released operation gate.");
        }
    }

    public void RestoreUiForUnloadOnFrameworkThread()
    {
        if (!PluginServices.Framework.IsInFrameworkUpdateThread)
            throw new InvalidOperationException("Unload UI restore must run on the framework update thread.");

        if (this.operationGate.Wait(0))
        {
            try
            {
                PluginServices.Log.Information("Unload restore acquired operation gate.");
                this.RestoreUi();
            }
            finally
            {
                this.operationGate.Release();
                PluginServices.Log.Information("Unload restore released operation gate.");
            }

            return;
        }

        PluginServices.Log.Warning("Capture or restore is active during unload; performing emergency UI restore on the framework thread.");
        this.RestoreUi();
    }

    private async Task<bool> BeginHideGameUiAsync(long operationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hidden = await PluginServices.Framework
            .RunOnFrameworkThread(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    lock (this.uiTransactionLock)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (this.unloadRequested)
                            throw new OperationCanceledException("Plugin unload requested before game UI hide.", cancellationToken);

                        var started = this.uiVisibilityController.TryBeginHideGameUi(out var error);
                        if (!started)
                            throw new InvalidOperationException(error ?? "Failed to hide game UI.");
                    }

                    PluginServices.Log.Information("Capture operation {OperationId} hid game UI.", operationId);
                    return true;
                })
            .ConfigureAwait(false);

        await PluginServices.Framework
            .DelayTicks(CaptureUiHideTimingPolicy.GameUiHidePresentationDelayTicks, cancellationToken)
            .ConfigureAwait(false);
        PluginServices.Log.Information(
            "Capture operation {OperationId} waited {Ticks} framework tick(s) for hidden game UI presentation.",
            operationId,
            CaptureUiHideTimingPolicy.GameUiHidePresentationDelayTicks);

        return hidden;
    }

    private async Task RestoreUiBestEffortAsync(long operationId)
    {
        try
        {
            await this.RestoreUiOnFrameworkThreadAsync().ConfigureAwait(false);
            PluginServices.Log.Information("Capture operation {OperationId} restored game UI.", operationId);
        }
        catch (Exception ex)
        {
            PluginServices.Log.Warning(ex, "Capture operation {OperationId} failed to restore game UI.", operationId);
        }
    }

    private Task RestoreUiOnFrameworkThreadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (PluginServices.Framework.IsInFrameworkUpdateThread)
        {
            this.RestoreUi();
            return Task.CompletedTask;
        }

        if (PluginServices.Framework.IsFrameworkUnloading)
            throw new InvalidOperationException("Cannot marshal UI restore while the framework is unloading from a non-framework thread.");

        return PluginServices.Framework.RunOnFrameworkThread(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                this.RestoreUi();
            });
    }

    private void RestoreUi()
    {
        lock (this.uiTransactionLock)
        {
            if (!this.uiVisibilityController.TryRestoreGameUi(out var error))
                throw new InvalidOperationException(error ?? "Failed to restore game UI.");
        }
    }

    private void ThrowIfUnloadRequested(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.uiTransactionLock)
        {
            if (this.unloadRequested)
                throw new OperationCanceledException("Plugin unload requested.", cancellationToken);
        }
    }
}
