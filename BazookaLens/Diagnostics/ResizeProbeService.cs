using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using BazookaLens.Commands;
using BazookaLens.Interop;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using NativeFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace BazookaLens.Diagnostics;

internal sealed class ResizeProbeService
{
    internal const int MaxWaitTicks = 180;
    internal const uint MaxDeviceRouteDimension = 8192;

    private readonly SemaphoreSlim operationGate = new(1, 1);

    internal async Task<T> RunExclusiveAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        await this.operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            this.operationGate.Release();
        }
    }

    public async Task<string> ProbeAsync(ResizeProbeOptions options, CancellationToken cancellationToken)
    {
        await this.operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            PluginServices.Log.Information("Resize probe started: {Options}", options);

            var before = await PluginServices.Framework
                .RunOnFrameworkThread(ResizeProbeRenderState.Capture)
                .ConfigureAwait(false);
            var config = await PluginServices.Framework
                .RunOnFrameworkThread(() => GameConfigSnapshot.Capture(PluginServices.GameConfig))
                .ConfigureAwait(false);
            var source = before.RequireDeviceTarget();
            var target = ResizeProbeTarget.FromScale(source.Width, source.Height, options.Scale);

            if (options.Route == ResizeProbeRoute.DryRun)
                return this.BuildDryRunText(options, config, before, source, target);

            ValidateMutableRouteTarget(target);

            ResizeProbeWaitResult? targetWait = null;
            ResizeProbeWaitResult? restoreWait = null;
            var targetOperationFailed = false;
            try
            {
                await this.ApplyMutableTargetAsync(options.Route, target).ConfigureAwait(false);

                targetWait = await WaitForTargetAsync(target, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                targetOperationFailed = true;
                throw;
            }
            finally
            {
                try
                {
                    await this.RestoreMutableTargetAsync(options.Route, source).ConfigureAwait(false);

                    if (!cancellationToken.IsCancellationRequested)
                        restoreWait = await WaitForTargetAsync(source, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex) when (targetOperationFailed)
                {
                    PluginServices.Log.Warning(ex, "Resize probe restore failed after the target operation failed; preserving the original failure.");
                }
            }

            return this.BuildMutableRouteText(options, config, before, source, target, targetWait, restoreWait);
        }
        finally
        {
            this.operationGate.Release();
        }
    }

    public async Task<string> RestoreDisplayModeAsync(RestoreDisplayOptions options, CancellationToken cancellationToken)
    {
        await this.operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            PluginServices.Log.Information("Display mode restore requested: {Options}", options);

            var before = await PluginServices.Framework
                .RunOnFrameworkThread(ResizeProbeRenderState.Capture)
                .ConfigureAwait(false);
            var config = await PluginServices.Framework
                .RunOnFrameworkThread(() => GameConfigSnapshot.Capture(PluginServices.GameConfig))
                .ConfigureAwait(false);
            var restoreMode = config.TryGetScreenMode()
                ?? throw new InvalidOperationException($"Cannot restore display mode because ScreenMode is unavailable: {config.ScreenMode}");

            await PluginServices.Framework
                .RunOnFrameworkThread(() => ApplyConfiguredWindowMode(restoreMode))
                .ConfigureAwait(false);
            await PluginServices.Framework.DelayTicks(30, cancellationToken).ConfigureAwait(false);

            var forceResult = options.ForceWindowRefresh
                ? await PluginServices.Framework
                    .RunOnFrameworkThread(() => ForceRefreshGameWindow(before.DeviceHwnd))
                    .ConfigureAwait(false)
                : "ForceWindowRefresh=False";

            if (options.ForceWindowRefresh)
                await PluginServices.Framework.DelayTicks(5, cancellationToken).ConfigureAwait(false);

            var after = await PluginServices.Framework
                .RunOnFrameworkThread(ResizeProbeRenderState.Capture)
                .ConfigureAwait(false);
            var afterConfig = await PluginServices.Framework
                .RunOnFrameworkThread(() => GameConfigSnapshot.Capture(PluginServices.GameConfig))
                .ConfigureAwait(false);
            var afterMode = afterConfig.TryGetScreenMode();

            var sb = new StringBuilder();
            sb.AppendLine("Display mode restore requested.");
            sb.AppendLine($"RequestedMode={restoreMode}");
            sb.AppendLine($"AfterMode={afterMode?.ToString() ?? "<unavailable>"}");
            sb.AppendLine($"AfterModeMatchesRequested={afterMode == restoreMode}");
            sb.AppendLine(forceResult);
            sb.Append(config.ToLogBlock("BeforeGameConfig"));
            sb.Append(afterConfig.ToLogBlock("AfterGameConfig"));
            sb.Append(before.ToLogBlock("Before"));
            sb.Append(after.ToLogBlock("After"));
            return sb.ToString();
        }
        finally
        {
            this.operationGate.Release();
        }
    }

    private static string ForceRefreshGameWindow(nint hWnd)
    {
        if (hWnd == nint.Zero)
            throw new InvalidOperationException("Cannot force-refresh display mode because the game window handle is unavailable.");

        var monitor = NativeMethods.MonitorFromWindow(hWnd, NativeMethods.MonitorDefaultToNearest);
        if (monitor == nint.Zero)
            throw new InvalidOperationException("MonitorFromWindow returned null for the game window.");

        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
            throw new InvalidOperationException($"GetMonitorInfoW failed: {Marshal.GetLastPInvokeError()}");

        var rect = info.Monitor;
        var resizeFlags = NativeMethods.SwpShowWindow | NativeMethods.SwpFrameChanged;
        if (!NativeMethods.SetWindowPos(hWnd, NativeMethods.HwndTopMost, rect.Left, rect.Top, rect.Width, rect.Height, resizeFlags))
            throw new InvalidOperationException($"SetWindowPos(HWND_TOPMOST, monitor rect) failed: {Marshal.GetLastPInvokeError()}");

        var pulseFlags = NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpShowWindow;
        if (!NativeMethods.SetWindowPos(hWnd, NativeMethods.HwndNoTopMost, 0, 0, 0, 0, pulseFlags))
            throw new InvalidOperationException($"SetWindowPos(HWND_NOTOPMOST) failed: {Marshal.GetLastPInvokeError()}");

        _ = NativeMethods.SetForegroundWindow(hWnd);
        _ = NativeMethods.SetFocus(hWnd);
        _ = NativeMethods.SetActiveWindow(hWnd);

        return string.Join(
            Environment.NewLine,
            "ForceWindowRefresh=True",
            $"  Hwnd=0x{hWnd.ToInt64():X}",
            $"  MonitorRect={rect.Left},{rect.Top},{rect.Width}x{rect.Height}",
            $"  WorkRect={info.Work.Left},{info.Work.Top},{info.Work.Width}x{info.Work.Height}",
            "  WindowPos=TopMost monitor rect, final NotTopMost; foreground/focus/active requested");
    }

    private Task ApplyMutableTargetAsync(ResizeProbeRoute route, ResizeProbeTarget target)
    {
        return route switch
        {
            ResizeProbeRoute.Device => PluginServices.Framework.RunOnFrameworkThread(() => ApplyDeviceTarget(target)),
            _ => throw new ArgumentOutOfRangeException(nameof(route), route, "Route does not support applying a mutable target."),
        };
    }

    private Task RestoreMutableTargetAsync(ResizeProbeRoute route, ResizeProbeTarget source)
    {
        return route switch
        {
            ResizeProbeRoute.Device => PluginServices.Framework.RunOnFrameworkThread(() => ApplyDeviceTarget(source)),
            _ => throw new ArgumentOutOfRangeException(nameof(route), route, "Route does not support restoring a mutable target."),
        };
    }

    internal static unsafe void ApplyDeviceTarget(ResizeProbeTarget target)
    {
        var device = Device.Instance();
        if (device is null)
            throw new InvalidOperationException("Device.Instance() returned null.");

        device->NewWidth = target.Width;
        device->NewHeight = target.Height;
        device->RequestResolutionChange = 1;

        PluginServices.Log.Information(
            "Device resize target requested: Width={Width}, Height={Height}, RequestResolutionChange={RequestResolutionChange}",
            target.Width,
            target.Height,
            device->RequestResolutionChange);
    }

    private static unsafe void ApplyConfiguredWindowMode(uint mode)
    {
        var framework = NativeFramework.Instance();
        if (framework is null)
            throw new InvalidOperationException("Framework.Instance() returned null.");

        var environmentManager = framework->EnvironmentManager;
        if (environmentManager is null)
            throw new InvalidOperationException("Framework.EnvironmentManager is null.");

        environmentManager->SetWindowMode(mode);

        PluginServices.Log.Information(
            "EnvironmentManager configured window mode restore queued: Mode={Mode}, UpdateOpCount={UpdateOpCount}",
            mode,
            environmentManager->UpdateOpCount);
    }

    internal static async Task<ResizeProbeWaitResult> WaitForTargetAsync(ResizeProbeTarget target, CancellationToken cancellationToken)
    {
        return await WaitForTargetAsync(target, static (state, target) => state.MatchesTarget(target), cancellationToken)
            .ConfigureAwait(false);
    }

    internal static async Task<ResizeProbeWaitResult> WaitForPresentationTargetAsync(ResizeProbeTarget target, CancellationToken cancellationToken)
    {
        return await WaitForTargetAsync(target, static (state, target) => state.MatchesPresentationTarget(target), cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<ResizeProbeWaitResult> WaitForTargetAsync(
        ResizeProbeTarget target,
        Func<ResizeProbeRenderState, ResizeProbeTarget, bool> matchesTarget,
        CancellationToken cancellationToken)
    {
        ResizeProbeRenderState? last = null;
        for (var tick = 1; tick <= MaxWaitTicks; tick++)
        {
            last = await PluginServices.Framework
                .RunOnTick(ResizeProbeRenderState.Capture, delayTicks: 1, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (matchesTarget(last, target))
                return new ResizeProbeWaitResult(true, tick, last);
        }

        return new ResizeProbeWaitResult(false, MaxWaitTicks, last ?? await PluginServices.Framework.RunOnFrameworkThread(ResizeProbeRenderState.Capture).ConfigureAwait(false));
    }

    internal static async Task<ResizeProbeSettleWaitResult> WaitForPresentationTargetSettleAsync(
        ResizeProbeTarget target,
        int minimumSettleTicks,
        int requiredStableTicks,
        CancellationToken cancellationToken)
    {
        return await WaitForTargetSettleAsync(
                target,
                minimumSettleTicks,
                requiredStableTicks,
                static (state, target) => state.MatchesPresentationTarget(target),
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal static async Task<ResizeProbeSettleWaitResult> WaitForTargetSettleAsync(
        ResizeProbeTarget target,
        int minimumSettleTicks,
        int requiredStableTicks,
        CancellationToken cancellationToken)
    {
        return await WaitForTargetSettleAsync(
                target,
                minimumSettleTicks,
                requiredStableTicks,
                static (state, target) => state.MatchesTarget(target),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<ResizeProbeSettleWaitResult> WaitForTargetSettleAsync(
        ResizeProbeTarget target,
        int minimumSettleTicks,
        int requiredStableTicks,
        Func<ResizeProbeRenderState, ResizeProbeTarget, bool> matchesTarget,
        CancellationToken cancellationToken)
    {
        if (minimumSettleTicks < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumSettleTicks), "Minimum settle ticks must be non-negative.");

        if (requiredStableTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(requiredStableTicks), "Required stable ticks must be positive.");

        ResizeProbeRenderState? last = null;
        var stableTicks = 0;
        var maxTicks = MaxWaitTicks + minimumSettleTicks + requiredStableTicks;

        for (var tick = 1; tick <= maxTicks; tick++)
        {
            last = await PluginServices.Framework
                .RunOnTick(ResizeProbeRenderState.Capture, delayTicks: 1, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            stableTicks = matchesTarget(last, target) ? stableTicks + 1 : 0;
            var decision = ResizeProbeSettlePolicy.Decide(tick, stableTicks, minimumSettleTicks, requiredStableTicks);
            if (decision.Ready)
                return new ResizeProbeSettleWaitResult(true, tick, stableTicks, decision.Reason, last);
        }

        return new ResizeProbeSettleWaitResult(
            false,
            maxTicks,
            stableTicks,
            ResizeProbeSettleWaitReason.TimedOut,
            last ?? await PluginServices.Framework.RunOnFrameworkThread(ResizeProbeRenderState.Capture).ConfigureAwait(false));
    }

    internal static void ValidateMutableRouteTarget(ResizeProbeTarget target)
    {
        if (target.Width > MaxDeviceRouteDimension || target.Height > MaxDeviceRouteDimension)
        {
            throw new InvalidOperationException(
                $"Resize probe target {target.Width}x{target.Height} exceeds the temporary safety limit of {MaxDeviceRouteDimension} pixels per dimension. Use dry-run first or a smaller scale.");
        }
    }

    private string BuildDryRunText(
        ResizeProbeOptions options,
        GameConfigSnapshot config,
        ResizeProbeRenderState before,
        ResizeProbeTarget source,
        ResizeProbeTarget target)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Resize probe dry-run.");
        this.AppendHeader(sb, options, source, target);
        sb.Append(config.ToLogBlock());
        sb.Append(before.ToLogBlock("Before"));
        return sb.ToString();
    }

    private string BuildMutableRouteText(
        ResizeProbeOptions options,
        GameConfigSnapshot config,
        ResizeProbeRenderState before,
        ResizeProbeTarget source,
        ResizeProbeTarget target,
        ResizeProbeWaitResult? targetWait,
        ResizeProbeWaitResult? restoreWait)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Resize probe {options.Route.ToString().ToLowerInvariant()} route completed.");
        this.AppendHeader(sb, options, source, target);
        sb.AppendLine($"TargetReached={targetWait?.Reached.ToString() ?? "<not-run>"}");
        sb.AppendLine($"TargetWaitTicks={targetWait?.Ticks.ToString() ?? "<not-run>"}");
        sb.AppendLine($"RestoreReached={restoreWait?.Reached.ToString() ?? "<not-run>"}");
        sb.AppendLine($"RestoreWaitTicks={restoreWait?.Ticks.ToString() ?? "<not-run>"}");
        sb.Append(config.ToLogBlock());
        sb.Append(before.ToLogBlock("Before"));

        if (targetWait is not null)
            sb.Append(targetWait.State.ToLogBlock("AfterTargetWait"));

        if (restoreWait is not null)
            sb.Append(restoreWait.State.ToLogBlock("AfterRestoreWait"));

        return sb.ToString();
    }

    private void AppendHeader(StringBuilder sb, ResizeProbeOptions options, ResizeProbeTarget source, ResizeProbeTarget target)
    {
        sb.AppendLine($"Route={options.Route}");
        sb.AppendLine($"Scale={options.Scale:0.###}");
        sb.AppendLine($"Source={source.Width}x{source.Height}");
        sb.AppendLine($"Target={target.Width}x{target.Height}");
        sb.AppendLine($"MaxDeviceRouteDimension={MaxDeviceRouteDimension}");
        sb.AppendLine($"DeviceRouteWithinLimit={target.Width <= MaxDeviceRouteDimension && target.Height <= MaxDeviceRouteDimension}");
    }

    internal sealed record ResizeProbeWaitResult(bool Reached, int Ticks, ResizeProbeRenderState State);

    internal sealed record ResizeProbeSettleWaitResult(
        bool Ready,
        int Ticks,
        int ConsecutiveStableTicks,
        ResizeProbeSettleWaitReason Reason,
        ResizeProbeRenderState State);
}
