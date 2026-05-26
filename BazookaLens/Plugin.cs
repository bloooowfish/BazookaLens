using System;
using System.Threading;
using System.Threading.Tasks;

using BazookaLens.Capture;
using BazookaLens.Commands;
using BazookaLens.Diagnostics;
using BazookaLens.UI;
using BazookaLens.Windows;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace BazookaLens;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/blens";

    [PluginService]
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    internal static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService]
    internal static IChatGui ChatGui { get; private set; } = null!;

    [PluginService]
    internal static IPluginLog Log { get; private set; } = null!;

    [PluginService]
    internal static IFramework Framework { get; private set; } = null!;

    [PluginService]
    internal static ITextureProvider TextureProvider { get; private set; } = null!;

    [PluginService]
    internal static ITextureReadbackProvider TextureReadbackProvider { get; private set; } = null!;

    [PluginService]
    internal static IGameConfig GameConfig { get; private set; } = null!;

    [PluginService]
    internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;

    [PluginService]
    internal static IKeyState KeyState { get; private set; } = null!;

    private readonly CancellationTokenSource unloadCts = new();
    private readonly WindowSystem windowSystem = new("BazookaLens");
    private readonly ReShadeEventBridge reShadeEventBridge;
    private readonly CaptureCoordinator captureCoordinator;
    private readonly CommandRouter commandRouter;
    private readonly Configuration configuration;
    private readonly CaptureSettingsProvider captureSettingsProvider;
    private readonly CaptureUiState captureUiState = new();
    private readonly OwnUiSuppressionController ownUiSuppressionController = new();
    private readonly CaptureRequestService captureRequestService;
    private readonly CapturePathService pathService;
    private readonly HotkeyService hotkeyService;
    private readonly BazookaLensWindow mainWindow;
    private readonly RegionOverlayWindow regionOverlayWindow;
    private readonly ShortcutCaptureOverlayWindow shortcutCaptureOverlayWindow;

    public Plugin()
    {
        PluginServices.Initialize(
            PluginInterface,
            CommandManager,
            ChatGui,
            Log,
            Framework,
            TextureProvider,
            TextureReadbackProvider,
            GameConfig,
            GameInteropProvider,
            KeyState);

        UiBuilderVisibilityPolicy.Apply(PluginInterface.UiBuilder);

        this.configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.configuration.Sanitize();

        Log.Information(
            "Bazooka Lens initializing: Version={Version}, ConfigDirectory={ConfigDirectory}, Assembly={Assembly}",
            PluginInterface.Manifest.AssemblyVersion,
            PluginInterface.ConfigDirectory.FullName,
            PluginInterface.AssemblyLocation.FullName);

        var statusService = new RuntimeStatusService();
        var reShadeExportProbe = new ReShadeExportProbe();
        this.reShadeEventBridge = new ReShadeEventBridge(reShadeExportProbe);
        var resizeProbeService = new ResizeProbeService();
        this.captureSettingsProvider = new CaptureSettingsProvider(this.configuration);
        this.pathService = new CapturePathService(() => this.captureSettingsProvider.ConfiguredSaveDirectory);
        var encoderService = new ImageEncoderService(() => this.configuration.ImageFormat);
        var viewportCaptureService = new ViewportCaptureService(encoderService, this.pathService);
        var uiVisibilityController = new UiVisibilityController();

        this.captureCoordinator = new CaptureCoordinator(viewportCaptureService, uiVisibilityController, resizeProbeService, this.reShadeEventBridge);
        this.regionOverlayWindow = new RegionOverlayWindow(this.configuration, this.ownUiSuppressionController);
        this.captureRequestService = new CaptureRequestService(
            this.captureSettingsProvider,
            this.captureCoordinator,
            this.ownUiSuppressionController,
            this.captureUiState,
            closeRegionEditorForCapture: this.regionOverlayWindow.CloseEditorForCapture);
        this.shortcutCaptureOverlayWindow = new ShortcutCaptureOverlayWindow(
            this.configuration,
            this.ownUiSuppressionController,
            this.captureUiState);
        this.mainWindow = new BazookaLensWindow(
            this.configuration,
            this.RunGuiCaptureAsync,
            this.captureUiState,
            this.pathService,
            this.regionOverlayWindow,
            this.shortcutCaptureOverlayWindow);

        this.windowSystem.AddWindow(this.mainWindow);
        this.windowSystem.AddWindow(this.shortcutCaptureOverlayWindow);
        this.windowSystem.AddWindow(this.regionOverlayWindow);
        this.ownUiSuppressionController.Register(this.mainWindow);
        this.ownUiSuppressionController.Register(this.regionOverlayWindow);
        this.ownUiSuppressionController.Register(this.shortcutCaptureOverlayWindow);
        PluginInterface.UiBuilder.Draw += this.windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += this.ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += this.ToggleMainUi;

        this.hotkeyService = new HotkeyService(
            KeyState,
            () => this.configuration.Shortcut,
            () => this.captureUiState.CanTriggerInteractiveCapture && !this.ownUiSuppressionController.IsSuppressed,
            this.RunHotkeyCaptureAsync);
        Framework.Update += this.hotkeyService.OnFrameworkUpdate;

        this.commandRouter = new CommandRouter(
            statusService,
            reShadeExportProbe,
            this.reShadeEventBridge,
            resizeProbeService,
            this.captureCoordinator,
            this.pathService,
            this.unloadCts.Token,
            this.captureRequestService,
            this.regionOverlayWindow.CloseEditorForCapture);

        CommandManager.AddHandler(CommandName, new CommandInfo(this.commandRouter.OnCommand)
        {
            HelpMessage = "Bazooka Lens render pipeline validation commands.",
        });

        Log.Information("Bazooka Lens initialized and command registered: {CommandName}", CommandName);
    }

    private void ToggleMainUi() => this.mainWindow.Toggle();

    private Task RunGuiCaptureAsync()
    {
        return this.RunConfiguredCaptureAsync("GUI", requireInteractiveAvailability: true);
    }

    private Task RunHotkeyCaptureAsync()
    {
        return this.RunConfiguredCaptureAsync("Hotkey", requireInteractiveAvailability: true);
    }

    private async Task RunConfiguredCaptureAsync(string source, bool requireInteractiveAvailability)
    {
        try
        {
            var output = await this.captureRequestService
                .CaptureFromConfiguredSettingsAsync(null, requireInteractiveAvailability, this.unloadCts.Token)
                .ConfigureAwait(false);
            PluginServices.ChatGui.Print($"Bazooka Lens: saved {Path.GetFileName(output)}.");
            PluginServices.Log.Information("{CaptureSource} capture saved: {Path}", source, output);
        }
        catch (OperationCanceledException) when (this.unloadCts.IsCancellationRequested)
        {
            PluginServices.Log.Information("{CaptureSource} capture cancelled because Bazooka Lens is unloading.", source);
        }
        catch (Exception ex)
        {
            PluginServices.Log.Error(ex, "{CaptureSource} capture failed.", source);
            PluginServices.ChatGui.PrintError($"Bazooka Lens {source.ToLowerInvariant()} capture failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        Log.Information("Bazooka Lens disposing: cancelling active operations.");
        this.captureCoordinator.RequestUnload();
        this.unloadCts.Cancel();

        Framework.Update -= this.hotkeyService.OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw -= this.windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= this.ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= this.ToggleMainUi;
        this.windowSystem.RemoveAllWindows();
        this.mainWindow.Dispose();
        this.regionOverlayWindow.Dispose();
        this.shortcutCaptureOverlayWindow.Dispose();

        try
        {
            this.reShadeEventBridge.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Best-effort ReShade event bridge disposal failed.");
        }

        try
        {
            if (Framework.IsInFrameworkUpdateThread)
            {
                this.captureCoordinator.RestoreUiForUnloadOnFrameworkThread();
            }
            else if (!Framework.IsFrameworkUnloading)
            {
                _ = this.captureCoordinator.RestoreUiAsync().ContinueWith(
                    task => Log.Warning(task.Exception, "Asynchronous unload UI restore failed."),
                    TaskContinuationOptions.OnlyOnFaulted);
            }
            else
            {
                Log.Warning("Skipped unload UI restore because Dispose is off the framework thread while the framework is unloading.");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Best-effort UI restore during unload failed.");
        }

        CommandManager.RemoveHandler(CommandName);
        this.unloadCts.Dispose();
        Log.Information("Bazooka Lens disposed and command removed: {CommandName}", CommandName);
    }
}
