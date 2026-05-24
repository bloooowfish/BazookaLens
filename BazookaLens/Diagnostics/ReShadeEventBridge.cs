using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using BazookaLens.Capture;
using BazookaLens.Interop;
using Dalamud.Hooking;

namespace BazookaLens.Diagnostics;

internal sealed unsafe class ReShadeEventBridge : IDisposable
{
    private const uint ReShadeApiVersion = 1;
    private const uint GetModuleHandleExFlagUnchangedRefcount = 0x00000002;
    private const uint GetModuleHandleExFlagFromAddress = 0x00000004;

    private readonly object gate = new();
    private readonly ReShadeExportProbe exportProbe;
    private readonly List<RegisteredReShadeEvent> registeredEvents = [];

    private ReShadeExports exports;
    private Hook<GetModuleHandleExWDelegate>? moduleResolverHook;
    private Hook<GetModuleFileNameWDelegate>? modulePathHook;
    private GetModuleHandleExWDelegate? moduleResolverDetour;
    private GetModuleFileNameWDelegate? modulePathDetour;
    private EffectRuntimeCallback? initEffectRuntimeCallback;
    private EffectRuntimeCallback? reloadedEffectsCallback;
    private EffectsCallback? beginEffectsCallback;
    private EffectsCallback? finishEffectsCallback;
    private PresetPathCallback? setCurrentPresetPathCallback;
    private nint[] callbackPointers = [];
    private bool active;
    private bool addonRegistered;
    private nint addonModuleHandle;
    private string? addonModulePath;
    private string? reShadeModuleName;
    private string? reShadeModulePath;
    private nint reShadeModuleBase;
    private DateTimeOffset? startedAtUtc;
    private string? lastEventName;
    private long totalEventCount;
    private long initEffectRuntimeCount;
    private long reloadedEffectsCount;
    private long beginEffectsCount;
    private long finishEffectsCount;
    private long setCurrentPresetPathCount;
    private long lastEventUnixMs;
    private long lastInitEffectRuntimeSequence;
    private long lastReloadedEffectsSequence;
    private long lastFinishEffectsSequence;
    private long lastSetCurrentPresetPathSequence;
    private long moduleResolverHitCount;
    private long resourceViewInspectionFailureCount;
    private string? lastFinishEffectsRtvDescription;
    private string? lastFinishEffectsRtvSrgbDescription;
    private ReShadePostEffectsCaptureRequest? pendingPostEffectsCaptureRequest;
    private long postEffectsCaptureCompletedCount;
    private string? lastPostEffectsCaptureError;

    public ReShadeEventBridge(ReShadeExportProbe exportProbe)
    {
        this.exportProbe = exportProbe;
    }

    public string Start()
    {
        lock (this.gate)
        {
            if (this.active)
            {
                PluginServices.Log.Information("ReShade event bridge start requested while already active.");
                return this.BuildStatusTextUnsafe("ReShade event bridge is already active.");
            }

            PluginServices.Log.Information("ReShade event bridge start requested.");
            this.ResetCountersUnsafe();

            try
            {
                var probeResult = this.exportProbe.Probe();
                var detectedModule = FindDetectedModule(probeResult);
                if (detectedModule is null)
                    throw new InvalidOperationException("ReShade addon exports are not available; run /blens reshade-status for module diagnostics.");

                var processModule = FindProcessModule(detectedModule.BaseAddress)
                    ?? throw new InvalidOperationException($"Detected ReShade module could not be resolved by base address: {FormatPointer(detectedModule.BaseAddress)}.");

                this.exports = LoadExports(detectedModule.BaseAddress);
                this.addonModuleHandle = Marshal.GetHINSTANCE(typeof(ReShadeEventBridge).Assembly.ManifestModule);
                if (this.addonModuleHandle == nint.Zero || this.addonModuleHandle == -1)
                    throw new InvalidOperationException($"Could not get a valid managed assembly module handle for ReShade addon registration: {FormatPointer(this.addonModuleHandle)}.");

                this.addonModulePath = PluginServices.PluginInterface.AssemblyLocation.FullName;
                this.reShadeModuleName = detectedModule.ModuleName;
                this.reShadeModulePath = detectedModule.ModulePath;
                this.reShadeModuleBase = detectedModule.BaseAddress;
                this.InstallModulePathHookUnsafe(processModule);

                PluginServices.Log.Information(
                    "Calling ReShadeRegisterAddon: ReShadeModule={ModuleName}, ReShadePath={ModulePath}, ReShadeBase={ReShadeBase}, AddonModuleHandle={AddonModuleHandle}, AddonModulePath={AddonModulePath}, ApiVersion={ApiVersion}",
                    this.reShadeModuleName,
                    this.reShadeModulePath ?? "<module-path-null>",
                    FormatPointer(this.reShadeModuleBase),
                    FormatPointer(this.addonModuleHandle),
                    this.addonModulePath,
                    ReShadeApiVersion);

                if (!this.exports.RegisterAddon(this.addonModuleHandle, ReShadeApiVersion))
                    throw new InvalidOperationException("ReShadeRegisterAddon returned false.");

                this.addonRegistered = true;
                PluginServices.Log.Information("ReShadeRegisterAddon succeeded.");

                this.CreateCallbacksUnsafe();
                this.InstallModuleResolverHookUnsafe(processModule);
                this.RegisterEventsUnsafe();

                this.active = true;
                this.startedAtUtc = DateTimeOffset.UtcNow;
                PluginServices.Log.Information("ReShade event bridge started.");
                return this.BuildStatusTextUnsafe("ReShade event bridge started.");
            }
            catch
            {
                this.StopUnsafe();
                throw;
            }
        }
    }

    public string Stop()
    {
        lock (this.gate)
        {
            if (!this.active && !this.addonRegistered)
            {
                PluginServices.Log.Information("ReShade event bridge stop requested while inactive.");
                return this.BuildStatusTextUnsafe("ReShade event bridge is not active.");
            }

            PluginServices.Log.Information("ReShade event bridge stop requested.");
            var summary = this.BuildStatusTextUnsafe("ReShade event bridge stopping snapshot before unregister.");
            this.StopUnsafe();
            return ReShadeEventBridgeStatusFormatter.AppendStoppedFooter(summary, this.active || this.addonRegistered);
        }
    }

    public string BuildStatusText()
    {
        lock (this.gate)
        {
            return this.BuildStatusTextUnsafe("ReShade event bridge status.");
        }
    }

    public ReShadeEventCounts GetEventCounts()
    {
        return new ReShadeEventCounts(
            Interlocked.Read(ref this.initEffectRuntimeCount),
            Interlocked.Read(ref this.reloadedEffectsCount),
            Interlocked.Read(ref this.beginEffectsCount),
            Interlocked.Read(ref this.finishEffectsCount));
    }

    public ReShadeEventSnapshot GetEventSnapshot()
    {
        return new ReShadeEventSnapshot(
            Interlocked.Read(ref this.initEffectRuntimeCount),
            Interlocked.Read(ref this.reloadedEffectsCount),
            Interlocked.Read(ref this.finishEffectsCount),
            Interlocked.Read(ref this.setCurrentPresetPathCount),
            Interlocked.Read(ref this.lastInitEffectRuntimeSequence),
            Interlocked.Read(ref this.lastReloadedEffectsSequence),
            Interlocked.Read(ref this.lastFinishEffectsSequence),
            Interlocked.Read(ref this.lastSetCurrentPresetPathSequence));
    }

    public bool IsActive
    {
        get
        {
            lock (this.gate)
            {
                return this.active;
            }
        }
    }

    public ReShadePostEffectsCaptureRequest ArmNextPostEffectsCapture(
        CaptureRegion? region,
        ReShadePostEffectsCaptureTarget? expectedTexture)
    {
        var request = new ReShadePostEffectsCaptureRequest(region, expectedTexture);
        lock (this.gate)
        {
            if (!this.active)
                throw new InvalidOperationException("ReShade event bridge is not active.");
            this.lastPostEffectsCaptureError = null;
        }

        if (Interlocked.CompareExchange(ref this.pendingPostEffectsCaptureRequest, request, null) is not null)
            throw new InvalidOperationException("A post-ReShade capture request is already pending.");

        PluginServices.Log.Information(
            "ReShade post-effects capture request armed: Region={Region}, ExpectedTexture={ExpectedTexture}",
            region?.ToString() ?? "<full>",
            expectedTexture?.ToString() ?? "<any>");

        return request;
    }

    public Task<D3D11ShaderResourceTextureWrap> WaitForPostEffectsCaptureAsync(
        ReShadePostEffectsCaptureRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        PluginServices.Log.Information(
            "ReShade post-effects capture request wait started: Region={Region}, ExpectedTexture={ExpectedTexture}, TimeoutMs={TimeoutMs}",
            request.Region?.ToString() ?? "<full>",
            request.ExpectedTexture?.ToString() ?? "<any>",
            timeout.TotalMilliseconds);

        return request.WaitAsync(timeout, cancellationToken, () => this.ClearPendingPostEffectsCaptureRequest(request));
    }

    public void CancelPostEffectsCapture(ReShadePostEffectsCaptureRequest request)
    {
        this.ClearPendingPostEffectsCaptureRequest(request);
        request.TryCancel();
    }

    public void Dispose()
    {
        lock (this.gate)
        {
            this.StopUnsafe();
        }
    }

    private static ReShadeModuleScanResult? FindDetectedModule(ReShadeExportProbeResult probeResult)
    {
        if (!probeResult.Detected)
            return null;

        return probeResult.CandidateModules.FirstOrDefault(candidate =>
            probeResult.RequiredExports.All(required => candidate.FoundExports.Contains(required, StringComparer.Ordinal)));
    }

    private static ProcessModule? FindProcessModule(nint baseAddress)
    {
        foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
        {
            if (module.BaseAddress == baseAddress)
                return module;
        }

        return null;
    }

    private void ClearPendingPostEffectsCaptureRequest(ReShadePostEffectsCaptureRequest request)
    {
        Interlocked.CompareExchange(ref this.pendingPostEffectsCaptureRequest, null, request);
    }

    private static ReShadeExports LoadExports(nint moduleBase)
    {
        var registerAddon = GetRequiredExport(moduleBase, "ReShadeRegisterAddon");
        var unregisterAddon = GetRequiredExport(moduleBase, "ReShadeUnregisterAddon");
        var registerEvent = GetRequiredExport(moduleBase, "ReShadeRegisterEvent");
        var unregisterEvent = GetRequiredExport(moduleBase, "ReShadeUnregisterEvent");

        return new ReShadeExports(
            (delegate* unmanaged<nint, uint, bool>)registerAddon,
            (delegate* unmanaged<nint, void>)unregisterAddon,
            (delegate* unmanaged<ReShadeAddonEvent, nint, void>)registerEvent,
            (delegate* unmanaged<ReShadeAddonEvent, nint, void>)unregisterEvent);
    }

    private static nint GetRequiredExport(nint moduleBase, string exportName)
    {
        var address = NativeMethods.GetProcAddress(moduleBase, exportName);
        if (address == nint.Zero)
            throw new MissingMethodException($"Required ReShade export was not found: {exportName}");

        return address;
    }

    private void CreateCallbacksUnsafe()
    {
        this.initEffectRuntimeCallback = runtime => this.RecordEvent(ReShadeObservedEvent.InitEffectRuntime, runtime);
        this.reloadedEffectsCallback = runtime => this.RecordEvent(ReShadeObservedEvent.ReShadeReloadedEffects, runtime);
        this.beginEffectsCallback = (runtime, commandList, rtv, rtvSrgb) =>
            this.RecordEvent(ReShadeObservedEvent.ReShadeBeginEffects, runtime, $"CommandList={FormatPointer(commandList)}, Rtv=0x{rtv:X}, RtvSrgb=0x{rtvSrgb:X}");
        this.finishEffectsCallback = (runtime, commandList, rtv, rtvSrgb) =>
            this.RecordFinishEffectsEvent(runtime, commandList, rtv, rtvSrgb);
        this.setCurrentPresetPathCallback = this.OnSetCurrentPresetPath;

        this.callbackPointers =
        [
            Marshal.GetFunctionPointerForDelegate(this.initEffectRuntimeCallback),
            Marshal.GetFunctionPointerForDelegate(this.reloadedEffectsCallback),
            Marshal.GetFunctionPointerForDelegate(this.beginEffectsCallback),
            Marshal.GetFunctionPointerForDelegate(this.finishEffectsCallback),
            Marshal.GetFunctionPointerForDelegate(this.setCurrentPresetPathCallback),
        ];

        PluginServices.Log.Information(
            "ReShade event bridge callbacks allocated: InitEffectRuntime={InitEffectRuntime}, ReloadedEffects={ReloadedEffects}, BeginEffects={BeginEffects}, FinishEffects={FinishEffects}, SetCurrentPresetPath={SetCurrentPresetPath}",
            FormatPointer(this.callbackPointers[0]),
            FormatPointer(this.callbackPointers[1]),
            FormatPointer(this.callbackPointers[2]),
            FormatPointer(this.callbackPointers[3]),
            FormatPointer(this.callbackPointers[4]));
    }

    private void InstallModuleResolverHookUnsafe(ProcessModule reShadeModule)
    {
        this.moduleResolverDetour = this.GetModuleHandleExWDetour;
        this.moduleResolverHook = PluginServices.GameInteropProvider.HookFromImport(
            reShadeModule,
            "kernel32.dll",
            "GetModuleHandleExW",
            0,
            this.moduleResolverDetour);
        this.moduleResolverHook.Enable();

        PluginServices.Log.Information(
            "ReShade GetModuleHandleExW import hook enabled: Address={Address}, Backend={Backend}",
            FormatPointer(this.moduleResolverHook.Address),
            this.moduleResolverHook.BackendName);
    }

    private void InstallModulePathHookUnsafe(ProcessModule reShadeModule)
    {
        this.modulePathDetour = this.GetModuleFileNameWDetour;
        this.modulePathHook = PluginServices.GameInteropProvider.HookFromImport(
            reShadeModule,
            "kernel32.dll",
            "GetModuleFileNameW",
            0,
            this.modulePathDetour);
        this.modulePathHook.Enable();

        PluginServices.Log.Information(
            "ReShade GetModuleFileNameW import hook enabled: Address={Address}, Backend={Backend}, AddonModulePath={AddonModulePath}",
            FormatPointer(this.modulePathHook.Address),
            this.modulePathHook.BackendName,
            this.addonModulePath ?? "<addon-module-path-null>");
    }

    private void RegisterEventsUnsafe()
    {
        this.RegisterEventUnsafe(ReShadeAddonEvent.InitEffectRuntime, this.callbackPointers[0], nameof(ReShadeAddonEvent.InitEffectRuntime));
        this.RegisterEventUnsafe(ReShadeAddonEvent.ReShadeReloadedEffects, this.callbackPointers[1], nameof(ReShadeAddonEvent.ReShadeReloadedEffects));
        this.RegisterEventUnsafe(ReShadeAddonEvent.ReShadeBeginEffects, this.callbackPointers[2], nameof(ReShadeAddonEvent.ReShadeBeginEffects));
        this.RegisterEventUnsafe(ReShadeAddonEvent.ReShadeFinishEffects, this.callbackPointers[3], nameof(ReShadeAddonEvent.ReShadeFinishEffects));
        this.RegisterEventUnsafe(ReShadeAddonEvent.ReShadeSetCurrentPresetPath, this.callbackPointers[4], nameof(ReShadeAddonEvent.ReShadeSetCurrentPresetPath));
    }

    private void RegisterEventUnsafe(ReShadeAddonEvent eventId, nint callbackPointer, string eventName)
    {
        this.exports.RegisterEvent(eventId, callbackPointer);
        this.registeredEvents.Add(new RegisteredReShadeEvent(eventId, callbackPointer, eventName));
        PluginServices.Log.Information(
            "ReShadeRegisterEvent completed: Event={EventName}, EventId={EventId}, Callback={Callback}",
            eventName,
            (uint)eventId,
            FormatPointer(callbackPointer));
    }

    private void StopUnsafe()
    {
        var pendingRequest = Interlocked.Exchange(ref this.pendingPostEffectsCaptureRequest, null);
        pendingRequest?.TryCancel();

        foreach (var registeredEvent in this.registeredEvents.AsEnumerable().Reverse())
        {
            try
            {
                if (this.exports.UnregisterEvent is not null)
                {
                    this.exports.UnregisterEvent(registeredEvent.EventId, registeredEvent.CallbackPointer);
                    PluginServices.Log.Information(
                        "ReShadeUnregisterEvent completed: Event={EventName}, EventId={EventId}, Callback={Callback}",
                        registeredEvent.EventName,
                        (uint)registeredEvent.EventId,
                        FormatPointer(registeredEvent.CallbackPointer));
                }
            }
            catch (Exception ex)
            {
                PluginServices.Log.Warning(ex, "ReShadeUnregisterEvent failed: Event={EventName}", registeredEvent.EventName);
            }
        }

        this.registeredEvents.Clear();

        if (this.addonRegistered)
        {
            try
            {
                this.exports.UnregisterAddon(this.addonModuleHandle);
                PluginServices.Log.Information("ReShadeUnregisterAddon completed: AddonModuleHandle={AddonModuleHandle}", FormatPointer(this.addonModuleHandle));
            }
            catch (Exception ex)
            {
                PluginServices.Log.Warning(ex, "ReShadeUnregisterAddon failed.");
            }
        }

        this.addonRegistered = false;

        this.DisposeModuleResolverHookUnsafe();
        this.DisposeModulePathHookUnsafe();

        this.moduleResolverHook = null;
        this.modulePathHook = null;
        this.moduleResolverDetour = null;
        this.modulePathDetour = null;
        this.initEffectRuntimeCallback = null;
        this.reloadedEffectsCallback = null;
        this.beginEffectsCallback = null;
        this.finishEffectsCallback = null;
        this.setCurrentPresetPathCallback = null;
        this.callbackPointers = [];
        this.exports = default;
        this.active = false;
        this.addonModuleHandle = nint.Zero;
        this.addonModulePath = null;
        this.reShadeModuleName = null;
        this.reShadeModulePath = null;
        this.reShadeModuleBase = nint.Zero;
        this.startedAtUtc = null;
    }

    private int GetModuleHandleExWDetour(uint dwFlags, ushort* lpModuleName, nint* phModule)
    {
        try
        {
            var requestedAddress = (nint)lpModuleName;
            if ((dwFlags & GetModuleHandleExFlagFromAddress) != 0 &&
                (dwFlags & GetModuleHandleExFlagUnchangedRefcount) != 0 &&
                ReShadeEventBridgeModuleResolver.ShouldResolve(requestedAddress, this.addonModuleHandle, this.callbackPointers))
            {
                *phModule = this.addonModuleHandle;
                var hitCount = Interlocked.Increment(ref this.moduleResolverHitCount);
                if (ReShadeEventBridgeLogPolicy.ShouldLogModuleResolverHit(hitCount))
                {
                    PluginServices.Log.Debug(
                        "ReShade GetModuleHandleExW addon module resolved: HitCount={HitCount}, RequestedAddress={RequestedAddress}, AddonModuleHandle={AddonModuleHandle}",
                        hitCount,
                        FormatPointer(requestedAddress),
                        FormatPointer(this.addonModuleHandle));
                }

                return 1;
            }
        }
        catch (Exception ex)
        {
            PluginServices.Log.Warning(ex, "ReShade GetModuleHandleExW detour failed before calling original.");
        }

        return this.moduleResolverHook?.Original(dwFlags, lpModuleName, phModule) ?? 0;
    }

    private uint GetModuleFileNameWDetour(nint hModule, char* lpFilename, uint nSize)
    {
        try
        {
            if (hModule == this.addonModuleHandle && !string.IsNullOrEmpty(this.addonModulePath))
            {
                var copied = CopyStringToBuffer(this.addonModulePath, lpFilename, nSize);
                PluginServices.Log.Debug(
                    "ReShade GetModuleFileNameW addon module path resolved: AddonModuleHandle={AddonModuleHandle}, AddonModulePath={AddonModulePath}, CopiedLength={CopiedLength}",
                    FormatPointer(this.addonModuleHandle),
                    this.addonModulePath,
                    copied);
                return copied;
            }
        }
        catch (Exception ex)
        {
            PluginServices.Log.Warning(ex, "ReShade GetModuleFileNameW detour failed before calling original.");
        }

        return this.modulePathHook?.Original(hModule, lpFilename, nSize) ?? 0;
    }

    private void OnSetCurrentPresetPath(nint runtime, byte* path)
    {
        string? presetPath = null;
        try
        {
            presetPath = path is null ? "<null>" : Marshal.PtrToStringUTF8((nint)path);
        }
        catch (Exception ex)
        {
            presetPath = $"<preset-path-error:{ex.GetType().Name}>";
        }

        this.RecordEvent(ReShadeObservedEvent.ReShadeSetCurrentPresetPath, runtime, $"PresetPath={presetPath ?? "<null>"}");
    }

    private void RecordFinishEffectsEvent(nint runtime, nint commandList, ulong rtv, ulong rtvSrgb)
    {
        this.lastFinishEffectsRtvDescription = $"View={FormatPointer(rtv)}";
        this.lastFinishEffectsRtvSrgbDescription = $"View={FormatPointer(rtvSrgb)}";

        this.RecordEvent(
            ReShadeObservedEvent.ReShadeFinishEffects,
            runtime,
            $"CommandList={FormatPointer(commandList)}, Rtv=0x{rtv:X}, RtvSrgb=0x{rtvSrgb:X}");

        this.TryCompletePostEffectsCapture(rtv, rtvSrgb);
    }

    private void TryCompletePostEffectsCapture(ulong rtv, ulong rtvSrgb)
    {
        var request = Volatile.Read(ref this.pendingPostEffectsCaptureRequest);
        if (request is null)
            return;

        try
        {
            var result = this.CopyPostEffectsTexture(rtv, rtvSrgb, request.Region, request.ExpectedTexture, out var skipped);
            if (result is null)
            {
                var skippedCount = skipped is { } skip
                    ? request.RecordTextureSizeMismatch(skip.ActualWidth, skip.ActualHeight)
                    : 0;
                if (ReShadeEventBridgeLogPolicy.ShouldLogSkippedPostEffectsCapture(skippedCount))
                {
                    PluginServices.Log.Debug(
                        "ReShade post-effects capture request skipped finish-effects texture: SkippedCount={SkippedCount}, ExpectedTexture={ExpectedTexture}, Reason={Reason}",
                        skippedCount,
                        request.ExpectedTexture?.ToString() ?? "<any>",
                        skipped?.Reason ?? "<none>");
                }

                return;
            }

            if (!ReferenceEquals(Interlocked.CompareExchange(ref this.pendingPostEffectsCaptureRequest, null, request), request))
            {
                result.Value.Texture.Dispose();
                return;
            }

            this.lastFinishEffectsRtvDescription = result.Value.PrimaryInspection?.ToString() ?? this.lastFinishEffectsRtvDescription;
            this.lastFinishEffectsRtvSrgbDescription = result.Value.SrgbInspection?.ToString() ?? this.lastFinishEffectsRtvSrgbDescription;

            if (request.TryComplete(result.Value.Texture))
            {
                var completedCount = Interlocked.Increment(ref this.postEffectsCaptureCompletedCount);
                PluginServices.Log.Information(
                    "ReShade post-effects capture request completed: CompletedCount={CompletedCount}, Texture={Width}x{Height}",
                    completedCount,
                    result.Value.Texture.Width,
                    result.Value.Texture.Height);
            }
            else
            {
                result.Value.Texture.Dispose();
            }
        }
        catch (Exception ex)
        {
            if (!ReferenceEquals(Interlocked.CompareExchange(ref this.pendingPostEffectsCaptureRequest, null, request), request))
                return;

            this.lastPostEffectsCaptureError = $"{ex.GetType().Name}: {ex.Message}";
            request.TrySetException(ex);
            PluginServices.Log.Warning(ex, "ReShade post-effects capture request failed.");
        }
    }

    private PostEffectsCopyResult? CopyPostEffectsTexture(
        ulong rtv,
        ulong rtvSrgb,
        CaptureRegion? region,
        ReShadePostEffectsCaptureTarget? expectedTexture,
        out ReShadePostEffectsCaptureSkip? skipped)
    {
        skipped = null;
        ReShadeResourceViewInspection? primaryInspection = null;
        try
        {
            var result = ReShadePostEffectsTextureCopier.CopyToShaderResourceTexture(rtv, region, expectedTexture);
            return new PostEffectsCopyResult(result.Texture, result.Inspection, SrgbInspection: null);
        }
        catch (ReShadePostEffectsTextureSizeMismatchException ex) when (rtvSrgb != 0 && rtvSrgb != rtv)
        {
            try
            {
                var result = ReShadePostEffectsTextureCopier.CopyToShaderResourceTexture(rtvSrgb, region, expectedTexture);
                return new PostEffectsCopyResult(result.Texture, PrimaryInspection: null, result.Inspection);
            }
            catch (ReShadePostEffectsTextureSizeMismatchException srgbEx)
            {
                skipped = new ReShadePostEffectsCaptureSkip(
                    srgbEx.ActualWidth,
                    srgbEx.ActualHeight,
                    $"{ex.Message}; {srgbEx.Message}");
                return null;
            }
        }
        catch (ReShadePostEffectsTextureSizeMismatchException ex)
        {
            skipped = new ReShadePostEffectsCaptureSkip(ex.ActualWidth, ex.ActualHeight, ex.Message);
            return null;
        }
        catch (Exception ex) when (rtvSrgb != 0 && rtvSrgb != rtv)
        {
            primaryInspection = ReShadeResourceViewInspection.Failure(rtv, $"{ex.GetType().Name}: {ex.Message}");
            Interlocked.Increment(ref this.resourceViewInspectionFailureCount);
            try
            {
                var result = ReShadePostEffectsTextureCopier.CopyToShaderResourceTexture(rtvSrgb, region, expectedTexture);
                return new PostEffectsCopyResult(result.Texture, primaryInspection, result.Inspection);
            }
            catch (ReShadePostEffectsTextureSizeMismatchException srgbEx)
            {
                skipped = new ReShadePostEffectsCaptureSkip(srgbEx.ActualWidth, srgbEx.ActualHeight, srgbEx.Message);
                return null;
            }
        }
    }

    private void RecordEvent(ReShadeObservedEvent eventType, nint runtime, string? detail = null)
    {
        try
        {
            var total = Interlocked.Increment(ref this.totalEventCount);
            switch (eventType)
            {
                case ReShadeObservedEvent.InitEffectRuntime:
                    Interlocked.Exchange(ref this.lastInitEffectRuntimeSequence, total);
                    break;
                case ReShadeObservedEvent.ReShadeReloadedEffects:
                    Interlocked.Exchange(ref this.lastReloadedEffectsSequence, total);
                    break;
                case ReShadeObservedEvent.ReShadeFinishEffects:
                    Interlocked.Exchange(ref this.lastFinishEffectsSequence, total);
                    break;
                case ReShadeObservedEvent.ReShadeSetCurrentPresetPath:
                    Interlocked.Exchange(ref this.lastSetCurrentPresetPathSequence, total);
                    break;
            }

            var eventCount = eventType switch
            {
                ReShadeObservedEvent.InitEffectRuntime => Interlocked.Increment(ref this.initEffectRuntimeCount),
                ReShadeObservedEvent.ReShadeReloadedEffects => Interlocked.Increment(ref this.reloadedEffectsCount),
                ReShadeObservedEvent.ReShadeBeginEffects => Interlocked.Increment(ref this.beginEffectsCount),
                ReShadeObservedEvent.ReShadeFinishEffects => Interlocked.Increment(ref this.finishEffectsCount),
                ReShadeObservedEvent.ReShadeSetCurrentPresetPath => Interlocked.Increment(ref this.setCurrentPresetPathCount),
                _ => 0,
            };
            var now = DateTimeOffset.UtcNow;
            Interlocked.Exchange(ref this.lastEventUnixMs, now.ToUnixTimeMilliseconds());
            this.lastEventName = eventType.ToString();

            if (ShouldLogObservedEvent(eventType, eventCount))
            {
                PluginServices.Log.Information(
                    "ReShade event received: Event={EventName}, EventCount={EventCount}, TotalEventCount={TotalEventCount}, Runtime={Runtime}, Detail={Detail}",
                    eventType,
                    eventCount,
                    total,
                    FormatPointer(runtime),
                    detail ?? "<none>");
            }
        }
        catch (Exception ex)
        {
            PluginServices.Log.Warning(ex, "ReShade event callback failed: Event={EventName}", eventType);
        }
    }

    private static bool ShouldLogObservedEvent(ReShadeObservedEvent eventType, long eventCount)
    {
        return eventType is ReShadeObservedEvent.ReShadeBeginEffects or ReShadeObservedEvent.ReShadeFinishEffects
            ? ReShadeEventBridgeLogPolicy.ShouldLogHighFrequencyEvent(eventCount)
            : ReShadeEventBridgeLogPolicy.ShouldLogLowFrequencyEvent(eventCount);
    }

    private void ResetCountersUnsafe()
    {
        this.lastEventName = null;
        Interlocked.Exchange(ref this.totalEventCount, 0);
        Interlocked.Exchange(ref this.initEffectRuntimeCount, 0);
        Interlocked.Exchange(ref this.reloadedEffectsCount, 0);
        Interlocked.Exchange(ref this.beginEffectsCount, 0);
        Interlocked.Exchange(ref this.finishEffectsCount, 0);
        Interlocked.Exchange(ref this.setCurrentPresetPathCount, 0);
        Interlocked.Exchange(ref this.lastEventUnixMs, 0);
        Interlocked.Exchange(ref this.lastInitEffectRuntimeSequence, 0);
        Interlocked.Exchange(ref this.lastReloadedEffectsSequence, 0);
        Interlocked.Exchange(ref this.lastFinishEffectsSequence, 0);
        Interlocked.Exchange(ref this.lastSetCurrentPresetPathSequence, 0);
        Interlocked.Exchange(ref this.moduleResolverHitCount, 0);
        Interlocked.Exchange(ref this.resourceViewInspectionFailureCount, 0);
        Interlocked.Exchange(ref this.postEffectsCaptureCompletedCount, 0);
        this.lastFinishEffectsRtvDescription = null;
        this.lastFinishEffectsRtvSrgbDescription = null;
        this.lastPostEffectsCaptureError = null;
    }

    private string BuildStatusTextUnsafe(string header)
    {
        var lastEventMs = Interlocked.Read(ref this.lastEventUnixMs);
        var sb = new StringBuilder();
        sb.AppendLine(header);
        sb.AppendLine($"Active={this.active}");
        sb.AppendLine($"AddonRegistered={this.addonRegistered}");
        sb.AppendLine($"StartedUtc={this.startedAtUtc?.ToString("O") ?? "<none>"}");
        sb.AppendLine($"ReShadeModule={this.reShadeModuleName ?? "<none>"}");
        sb.AppendLine($"ReShadeModulePath={this.reShadeModulePath ?? "<none>"}");
        sb.AppendLine($"ReShadeModuleBase={FormatPointer(this.reShadeModuleBase)}");
        sb.AppendLine($"AddonModuleHandle={FormatPointer(this.addonModuleHandle)}");
        sb.AppendLine($"AddonModulePath={this.addonModulePath ?? "<none>"}");
        sb.AppendLine($"ModulePathHook={(this.modulePathHook is null ? "<none>" : $"{this.modulePathHook.BackendName}, Enabled={this.modulePathHook.IsEnabled}")}");
        sb.AppendLine($"ModuleResolverHook={(this.moduleResolverHook is null ? "<none>" : $"{this.moduleResolverHook.BackendName}, Enabled={this.moduleResolverHook.IsEnabled}")}");
        sb.AppendLine($"ModuleResolverHitCount={Interlocked.Read(ref this.moduleResolverHitCount)}");
        sb.AppendLine($"RegisteredEvents={string.Join(",", this.registeredEvents.Select(item => item.EventName))}");
        sb.AppendLine($"TotalEventCount={Interlocked.Read(ref this.totalEventCount)}");
        sb.AppendLine($"InitEffectRuntimeCount={Interlocked.Read(ref this.initEffectRuntimeCount)}");
        sb.AppendLine($"ReShadeReloadedEffectsCount={Interlocked.Read(ref this.reloadedEffectsCount)}");
        sb.AppendLine($"ReShadeBeginEffectsCount={Interlocked.Read(ref this.beginEffectsCount)}");
        sb.AppendLine($"ReShadeFinishEffectsCount={Interlocked.Read(ref this.finishEffectsCount)}");
        sb.AppendLine($"ReShadeSetCurrentPresetPathCount={Interlocked.Read(ref this.setCurrentPresetPathCount)}");
        sb.AppendLine($"ResourceViewInspectionFailureCount={Interlocked.Read(ref this.resourceViewInspectionFailureCount)}");
        sb.AppendLine($"LastFinishEffectsRtv={this.lastFinishEffectsRtvDescription ?? "<none>"}");
        sb.AppendLine($"LastFinishEffectsRtvSrgb={this.lastFinishEffectsRtvSrgbDescription ?? "<none>"}");
        sb.AppendLine($"PostEffectsCapturePending={this.pendingPostEffectsCaptureRequest is not null}");
        sb.AppendLine($"PostEffectsCaptureCompletedCount={Interlocked.Read(ref this.postEffectsCaptureCompletedCount)}");
        sb.AppendLine($"LastPostEffectsCaptureError={this.lastPostEffectsCaptureError ?? "<none>"}");
        sb.AppendLine($"LastEventName={this.lastEventName ?? "<none>"}");
        sb.AppendLine($"LastEventUtc={(lastEventMs > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(lastEventMs).ToString("O") : "<none>")}");
        return sb.ToString();
    }

    private static string FormatPointer(nint pointer)
    {
        return $"0x{pointer.ToInt64():X}";
    }

    private static string FormatPointer(ulong pointer)
    {
        return $"0x{pointer:X}";
    }

    private static uint CopyStringToBuffer(string value, char* destination, uint destinationLength)
    {
        if (destination is null || destinationLength == 0)
            return 0;

        var length = Math.Min(value.Length, (int)destinationLength - 1);
        for (var i = 0; i < length; i++)
            destination[i] = value[i];

        destination[length] = '\0';
        return (uint)length;
    }

    private void DisposeModuleResolverHookUnsafe()
    {
        if (this.moduleResolverHook is null)
            return;

        try
        {
            if (this.moduleResolverHook.IsEnabled)
                this.moduleResolverHook.Disable();

            this.moduleResolverHook.Dispose();
            PluginServices.Log.Information("ReShade GetModuleHandleExW import hook disposed.");
        }
        catch (Exception ex)
        {
            PluginServices.Log.Warning(ex, "ReShade GetModuleHandleExW import hook disposal failed.");
        }
    }

    private void DisposeModulePathHookUnsafe()
    {
        if (this.modulePathHook is null)
            return;

        try
        {
            if (this.modulePathHook.IsEnabled)
                this.modulePathHook.Disable();

            this.modulePathHook.Dispose();
            PluginServices.Log.Information("ReShade GetModuleFileNameW import hook disposed.");
        }
        catch (Exception ex)
        {
            PluginServices.Log.Warning(ex, "ReShade GetModuleFileNameW import hook disposal failed.");
        }
    }

    private readonly record struct RegisteredReShadeEvent(ReShadeAddonEvent EventId, nint CallbackPointer, string EventName);

    private readonly struct ReShadeExports(
        delegate* unmanaged<nint, uint, bool> registerAddon,
        delegate* unmanaged<nint, void> unregisterAddon,
        delegate* unmanaged<ReShadeAddonEvent, nint, void> registerEvent,
        delegate* unmanaged<ReShadeAddonEvent, nint, void> unregisterEvent)
    {
        public readonly delegate* unmanaged<nint, uint, bool> RegisterAddon = registerAddon;

        public readonly delegate* unmanaged<nint, void> UnregisterAddon = unregisterAddon;

        public readonly delegate* unmanaged<ReShadeAddonEvent, nint, void> RegisterEvent = registerEvent;

        public readonly delegate* unmanaged<ReShadeAddonEvent, nint, void> UnregisterEvent = unregisterEvent;
    }

    private enum ReShadeAddonEvent : uint
    {
        InitEffectRuntime = 9,
        ReShadeBeginEffects = 76,
        ReShadeFinishEffects = 77,
        ReShadeReloadedEffects = 78,
        ReShadeSetCurrentPresetPath = 84,
    }

    private enum ReShadeObservedEvent
    {
        InitEffectRuntime,
        ReShadeBeginEffects,
        ReShadeFinishEffects,
        ReShadeReloadedEffects,
        ReShadeSetCurrentPresetPath,
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void EffectRuntimeCallback(nint runtime);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void EffectsCallback(nint runtime, nint commandList, ulong rtv, ulong rtvSrgb);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void PresetPathCallback(nint runtime, byte* path);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GetModuleHandleExWDelegate(uint dwFlags, ushort* lpModuleName, nint* phModule);

    private delegate uint GetModuleFileNameWDelegate(nint hModule, char* lpFilename, uint nSize);

    private readonly record struct PostEffectsCopyResult(
        D3D11ShaderResourceTextureWrap Texture,
        ReShadeResourceViewInspection? PrimaryInspection,
        ReShadeResourceViewInspection? SrgbInspection);
}
