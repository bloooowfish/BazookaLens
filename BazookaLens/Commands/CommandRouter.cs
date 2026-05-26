using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using BazookaLens.Capture;
using BazookaLens.Diagnostics;

namespace BazookaLens.Commands;

internal readonly record struct BlensCommandHelpEntry(
    string Usage,
    string SampleInvocation,
    BlensCommand ExpectedCommand);

internal sealed class CommandRouter
{
    public static readonly IReadOnlyList<BlensCommandHelpEntry> HelpEntries =
    [
        new("shoot [scale] (configured scale unless overridden, after, hide-ui, post-ReShade when available)", "shoot 1.5", BlensCommand.Shoot),
        new("open-folder", "open-folder", BlensCommand.OpenFolder),
        new("status", "status", BlensCommand.Status),
        new("reshade-status", "reshade-status", BlensCommand.ReShadeStatus),
        new("reshade-events start|stop|status", "reshade-events status", BlensCommand.ReShadeEvents),
        new("capture [before|after] [hide-ui]", "capture after hide-ui", BlensCommand.Capture),
        new("capture-scale scale [before|after] [hide-ui]", "capture-scale 1.5 after hide-ui", BlensCommand.Capture),
        new("capture-region x y w h [before|after] [hide-ui]", "capture-region 100 100 400 300 after hide-ui", BlensCommand.Capture),
        new("capture-region-scale x y w h scale [before|after] [hide-ui]", "capture-region-scale 100 100 400 300 1.5 after hide-ui", BlensCommand.Capture),
        new("resize-probe scale [dry-run|device]", "resize-probe 1.5 dry-run", BlensCommand.ResizeProbe),
        new("restore-ui", "restore-ui", BlensCommand.RestoreUi),
        new("restore-display [force]", "restore-display force", BlensCommand.RestoreDisplay),
        new("help", "help", BlensCommand.Help),
    ];

    public static readonly string HelpText =
        "Bazooka Lens: " + string.Join(" | ", HelpEntries.Select(entry => $"/blens {entry.Usage}"));

    private readonly RuntimeStatusService statusService;
    private readonly ReShadeExportProbe reShadeExportProbe;
    private readonly ReShadeEventBridge reShadeEventBridge;
    private readonly ResizeProbeService resizeProbeService;
    private readonly CaptureCoordinator captureCoordinator;
    private readonly CapturePathService pathService;
    private readonly CaptureRequestService? captureRequestService;
    private readonly Action closeRegionEditorForCapture;
    private readonly CancellationToken unloadToken;
    private long commandSequence;

    public CommandRouter(
        RuntimeStatusService statusService,
        ReShadeExportProbe reShadeExportProbe,
        ReShadeEventBridge reShadeEventBridge,
        ResizeProbeService resizeProbeService,
        CaptureCoordinator captureCoordinator,
        CapturePathService pathService,
        CancellationToken unloadToken,
        CaptureRequestService? captureRequestService = null,
        Action? closeRegionEditorForCapture = null)
    {
        this.statusService = statusService;
        this.reShadeExportProbe = reShadeExportProbe;
        this.reShadeEventBridge = reShadeEventBridge;
        this.resizeProbeService = resizeProbeService;
        this.captureCoordinator = captureCoordinator;
        this.pathService = pathService;
        this.unloadToken = unloadToken;
        this.captureRequestService = captureRequestService;
        this.closeRegionEditorForCapture = closeRegionEditorForCapture ?? (() => { });
    }

    public static ParsedBlensCommand Parse(string args)
    {
        var tokens = SplitArgs(args);
        if (tokens.Length == 0)
            return new ParsedBlensCommand(BlensCommand.Help);

        return tokens[0].ToLowerInvariant() switch
        {
            "help" => new ParsedBlensCommand(BlensCommand.Help),
            "status" => new ParsedBlensCommand(BlensCommand.Status),
            "reshade-status" => new ParsedBlensCommand(BlensCommand.ReShadeStatus),
            "reshade-events" => new ParsedBlensCommand(BlensCommand.ReShadeEvents, ReShadeEventsAction: ParseReShadeEvents(tokens)),
            "resize-probe" => new ParsedBlensCommand(BlensCommand.ResizeProbe, ResizeProbeOptions: ParseResizeProbe(tokens)),
            "restore-ui" => new ParsedBlensCommand(BlensCommand.RestoreUi),
            "restore-display" => new ParsedBlensCommand(BlensCommand.RestoreDisplay, RestoreDisplayOptions: ParseRestoreDisplay(tokens)),
            "capture" => new ParsedBlensCommand(BlensCommand.Capture, ParseCapture(tokens, regionStart: null, scaleIndex: null)),
            "capture-region" => new ParsedBlensCommand(BlensCommand.Capture, ParseCapture(tokens, regionStart: 1, scaleIndex: null)),
            "capture-scale" => new ParsedBlensCommand(BlensCommand.Capture, ParseCapture(tokens, regionStart: null, scaleIndex: 1)),
            "capture-region-scale" => new ParsedBlensCommand(BlensCommand.Capture, ParseCapture(tokens, regionStart: 1, scaleIndex: 5)),
            "shoot" => new ParsedBlensCommand(BlensCommand.Shoot, ShootScaleOverride: ParseShoot(tokens)),
            "open-folder" => new ParsedBlensCommand(BlensCommand.OpenFolder, ParseNoOptions(tokens, "open-folder")),
            _ => throw new ArgumentException($"Unknown /blens command: {tokens[0]}", nameof(args)),
        };
    }

    public void OnCommand(string command, string args)
    {
        var commandId = Interlocked.Increment(ref this.commandSequence);
        PluginServices.Log.Information("Command {CommandId} received: Command={Command}, Args={Args}", commandId, command, args);

        ParsedBlensCommand parsed;
        try
        {
            parsed = Parse(args);
            PluginServices.Log.Information(
                "Command {CommandId} parsed: ParsedCommand={ParsedCommand}, Options={Options}",
                commandId,
                parsed.Command,
                FormatParsedOptions(parsed));
        }
        catch (Exception ex)
        {
            PluginServices.Log.Warning(ex, "Command {CommandId} parse failed.", commandId);
            PluginServices.ChatGui.PrintError($"Bazooka Lens command failed: {ex.Message}");
            return;
        }

        switch (parsed.Command)
        {
            case BlensCommand.Help:
                this.PrintHelp(commandId);
                break;
            case BlensCommand.Status:
                _ = this.RunStatusCommandAsync(commandId);
                break;
            case BlensCommand.ReShadeStatus:
                this.RunReShadeStatusCommand(commandId);
                break;
            case BlensCommand.ReShadeEvents:
                this.RunReShadeEventsCommand(commandId, parsed.ReShadeEventsAction!.Value);
                break;
            case BlensCommand.ResizeProbe:
                _ = this.RunResizeProbeCommandAsync(commandId, parsed.ResizeProbeOptions!);
                break;
            case BlensCommand.Capture:
                _ = this.RunCaptureCommandAsync(commandId, parsed.CaptureOptions!);
                break;
            case BlensCommand.Shoot:
                _ = this.RunShootCommandAsync(commandId, parsed.ShootScaleOverride);
                break;
            case BlensCommand.OpenFolder:
                this.RunOpenFolderCommand(commandId);
                break;
            case BlensCommand.RestoreUi:
                _ = this.RunRestoreUiCommandAsync(commandId);
                break;
            case BlensCommand.RestoreDisplay:
                _ = this.RunRestoreDisplayCommandAsync(commandId, parsed.RestoreDisplayOptions!);
                break;
            default:
                PluginServices.Log.Warning("Command {CommandId} reached unexpected parsed command: {ParsedCommand}", commandId, parsed.Command);
                PluginServices.ChatGui.PrintError("Bazooka Lens command failed: unexpected command.");
                break;
        }
    }

    private static CaptureOptions ParseCapture(IReadOnlyList<string> tokens, int? regionStart, int? scaleIndex)
    {
        var optionStart = 1;
        CaptureRegion? region = null;
        if (regionStart is int start)
        {
            if (tokens.Count < start + 4)
                throw new ArgumentException("capture-region requires x y w h.");

            region = new CaptureRegion(
                ParseInt(tokens[start], "x"),
                ParseInt(tokens[start + 1], "y"),
                ParseInt(tokens[start + 2], "w"),
                ParseInt(tokens[start + 3], "h"));
            optionStart = start + 4;
        }

        var scale = 1.0;
        if (scaleIndex is int scaleStart)
        {
            if (tokens.Count <= scaleStart)
                throw new ArgumentException($"{tokens[0]} requires a scale.");

            scale = ParseScale(tokens[scaleStart], tokens[0]);
            optionStart = scaleStart + 1;
        }

        var timing = CaptureTiming.AfterImGui;
        var timingSet = false;
        var hideUi = false;

        for (var i = optionStart; i < tokens.Count; i++)
        {
            switch (tokens[i].ToLowerInvariant())
            {
                case "before":
                    SetTiming(CaptureTiming.BeforeImGui);
                    break;
                case "after":
                    SetTiming(CaptureTiming.AfterImGui);
                    break;
                case "hide-ui":
                    if (hideUi)
                        throw new ArgumentException("hide-ui was specified more than once.");

                    hideUi = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown capture option: {tokens[i]}");
            }
        }

        return new CaptureOptions(timing, hideUi, region, scale);

        void SetTiming(CaptureTiming value)
        {
            if (timingSet)
                throw new ArgumentException("Capture timing was specified more than once.");

            timing = value;
            timingSet = true;
        }
    }

    private static double? ParseShoot(IReadOnlyList<string> tokens)
    {
        if (tokens.Count > 2)
            throw new ArgumentException("shoot accepts at most one optional scale.");

        return tokens.Count == 2
            ? ParseScale(tokens[1], "shoot")
            : null;
    }

    private static CaptureOptions? ParseNoOptions(IReadOnlyList<string> tokens, string commandName)
    {
        if (tokens.Count != 1)
            throw new ArgumentException($"{commandName} does not accept options.");

        return null;
    }

    private static ReShadeEventsAction ParseReShadeEvents(IReadOnlyList<string> tokens)
    {
        if (tokens.Count != 2)
            throw new ArgumentException("reshade-events requires exactly one action: start, stop, or status.");

        return tokens[1].ToLowerInvariant() switch
        {
            "start" => ReShadeEventsAction.Start,
            "stop" => ReShadeEventsAction.Stop,
            "status" => ReShadeEventsAction.Status,
            _ => throw new ArgumentException($"Unknown reshade-events action: {tokens[1]}"),
        };
    }

    private static ResizeProbeOptions ParseResizeProbe(IReadOnlyList<string> tokens)
    {
        if (tokens.Count is < 2 or > 3)
            throw new ArgumentException("resize-probe requires a scale and optional route: dry-run or device.");

        var scale = ParseScale(tokens[1], "resize-probe");

        var route = ResizeProbeRoute.DryRun;
        if (tokens.Count == 3)
        {
            route = tokens[2].ToLowerInvariant() switch
            {
                "dry-run" => ResizeProbeRoute.DryRun,
                "device" => ResizeProbeRoute.Device,
                _ => throw new ArgumentException($"Unknown resize-probe route: {tokens[2]}"),
            };
        }

        return new ResizeProbeOptions(scale, route);
    }

    private static RestoreDisplayOptions ParseRestoreDisplay(IReadOnlyList<string> tokens)
    {
        if (tokens.Count > 2)
            throw new ArgumentException("restore-display accepts at most one option: force.");

        if (tokens.Count == 1)
            return new RestoreDisplayOptions(false);

        return tokens[1].ToLowerInvariant() switch
        {
            "force" => new RestoreDisplayOptions(true),
            _ => throw new ArgumentException($"Unknown restore-display option: {tokens[1]}"),
        };
    }

    private static int ParseInt(string value, string name)
    {
        if (!int.TryParse(value, out var parsed))
            throw new ArgumentException($"capture-region {name} must be an integer.");

        return parsed;
    }

    private static double ParseScale(string value, string commandName)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale))
            throw new ArgumentException($"{commandName} scale must be a number.");

        scale = CaptureScalePolicy.Normalize(scale);
        if (!CaptureScalePolicy.IsValid(scale, out _))
            throw new ArgumentException(CaptureScalePolicy.FormatRangeError($"{commandName} scale"), nameof(value));

        return scale;
    }

    private static string[] SplitArgs(string args)
    {
        return args.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static string FormatParsedOptions(ParsedBlensCommand parsed)
    {
        return parsed.CaptureOptions?.ToString()
            ?? parsed.ResizeProbeOptions?.ToString()
            ?? parsed.RestoreDisplayOptions?.ToString()
            ?? parsed.ReShadeEventsAction?.ToString()
            ?? "<none>";
    }

    private void PrintHelp(long commandId)
    {
        PluginServices.Log.Information("Command {CommandId} printing help.", commandId);
        PluginServices.ChatGui.Print(HelpText);
    }

    private async Task RunStatusCommandAsync(long commandId)
    {
        try
        {
            PluginServices.Log.Information("Command {CommandId} status snapshot started.", commandId);
            var status = await PluginServices.Framework
                .RunOnFrameworkThread(() => this.statusService.BuildStatusText())
                .ConfigureAwait(false);

            PluginServices.Log.Information("Command {CommandId} status snapshot:\n{Status}", commandId, status);
            PluginServices.ChatGui.Print("Bazooka Lens: status written to /xllog.");
        }
        catch (Exception ex)
        {
            PluginServices.Log.Error(ex, "Command {CommandId} status failed.", commandId);
            PluginServices.ChatGui.PrintError($"Bazooka Lens status failed: {ex.Message}");
        }
    }

    private void RunReShadeStatusCommand(long commandId)
    {
        try
        {
            PluginServices.Log.Information("Command {CommandId} ReShade status probe started.", commandId);
            var reshade = this.reShadeExportProbe.Format();
            PluginServices.Log.Information("Command {CommandId} ReShade status: {ReShadeStatus}", commandId, reshade);
            PluginServices.ChatGui.Print($"Bazooka Lens: {reshade}");
        }
        catch (Exception ex)
        {
            PluginServices.Log.Error(ex, "Command {CommandId} ReShade status failed.", commandId);
            PluginServices.ChatGui.PrintError($"Bazooka Lens ReShade status failed: {ex.Message}");
        }
    }

    private void RunReShadeEventsCommand(long commandId, ReShadeEventsAction action)
    {
        try
        {
            PluginServices.Log.Information("Command {CommandId} ReShade events action started: Action={Action}", commandId, action);
            var status = action switch
            {
                ReShadeEventsAction.Start => this.reShadeEventBridge.Start(),
                ReShadeEventsAction.Stop => this.reShadeEventBridge.Stop(),
                ReShadeEventsAction.Status => this.reShadeEventBridge.BuildStatusText(),
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown ReShade events action."),
            };

            PluginServices.Log.Information("Command {CommandId} ReShade events action result:\n{Status}", commandId, status);
            PluginServices.ChatGui.Print($"Bazooka Lens: ReShade events {action.ToString().ToLowerInvariant()} complete; details written to /xllog.");
        }
        catch (Exception ex)
        {
            PluginServices.Log.Error(ex, "Command {CommandId} ReShade events action failed: Action={Action}", commandId, action);
            PluginServices.ChatGui.PrintError($"Bazooka Lens ReShade events {action.ToString().ToLowerInvariant()} failed: {ex.Message}");
        }
    }

    private async Task RunResizeProbeCommandAsync(long commandId, ResizeProbeOptions options)
    {
        try
        {
            PluginServices.Log.Information("Command {CommandId} resize probe started: {Options}", commandId, options);
            var status = await this.resizeProbeService.ProbeAsync(options, this.unloadToken).ConfigureAwait(false);

            PluginServices.Log.Information("Command {CommandId} resize probe result:\n{Status}", commandId, status);
            PluginServices.ChatGui.Print($"Bazooka Lens: resize-probe {options.Route.ToString().ToLowerInvariant()} complete; details written to /xllog.");
        }
        catch (OperationCanceledException) when (this.unloadToken.IsCancellationRequested)
        {
            this.LogCommandCanceledForUnload(commandId, "resize probe");
        }
        catch (Exception ex)
        {
            PluginServices.Log.Error(ex, "Command {CommandId} resize probe failed.", commandId);
            PluginServices.ChatGui.PrintError($"Bazooka Lens resize-probe failed: {ex.Message}");
        }
    }

    private async Task RunCaptureCommandAsync(long commandId, CaptureOptions options)
    {
        try
        {
            PluginServices.Log.Information("Command {CommandId} capture started: {Options}", commandId, options);
            this.closeRegionEditorForCapture();
            var output = await this.captureCoordinator.CaptureAsync(options, this.unloadToken).ConfigureAwait(false);

            PluginServices.ChatGui.Print($"Bazooka Lens: saved {Path.GetFileName(output)}. Use /blens open-folder to view captures.");
            PluginServices.Log.Information("Command {CommandId} capture saved: {Path}", commandId, output);
        }
        catch (OperationCanceledException) when (this.unloadToken.IsCancellationRequested)
        {
            this.LogCommandCanceledForUnload(commandId, "capture");
        }
        catch (Exception ex)
        {
            PluginServices.Log.Error(ex, "Command {CommandId} capture failed.", commandId);
            PluginServices.ChatGui.PrintError($"Bazooka Lens capture failed: {ex.Message}");
        }
    }

    private async Task RunShootCommandAsync(long commandId, double? scaleOverride)
    {
        try
        {
            if (this.captureRequestService is null)
                throw new InvalidOperationException("Settings-backed shoot is not available before plugin configuration is loaded.");

            PluginServices.Log.Information(
                "Command {CommandId} settings-backed shoot started: ScaleOverride={ScaleOverride}",
                commandId,
                scaleOverride?.ToString(CultureInfo.InvariantCulture) ?? "<configured>");
            var output = await this.captureRequestService
                .CaptureFromConfiguredSettingsAsync(scaleOverride, requireInteractiveAvailability: false, cancellationToken: this.unloadToken)
                .ConfigureAwait(false);

            PluginServices.ChatGui.Print($"Bazooka Lens: saved {Path.GetFileName(output)}. Use /blens open-folder to view captures.");
            PluginServices.Log.Information("Command {CommandId} shoot saved: {Path}", commandId, output);
        }
        catch (OperationCanceledException) when (this.unloadToken.IsCancellationRequested)
        {
            this.LogCommandCanceledForUnload(commandId, "shoot");
        }
        catch (Exception ex)
        {
            PluginServices.Log.Error(ex, "Command {CommandId} shoot failed.", commandId);
            PluginServices.ChatGui.PrintError($"Bazooka Lens shoot failed: {ex.Message}");
        }
    }

    private void RunOpenFolderCommand(long commandId)
    {
        try
        {
            var directory = this.pathService.GetScreenshotDirectory();
            PluginServices.Log.Information("Command {CommandId} opening screenshot folder: {Directory}", commandId, directory);
            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true,
            });

            PluginServices.ChatGui.Print($"Bazooka Lens: opened screenshot folder {directory}");
        }
        catch (Exception ex)
        {
            PluginServices.Log.Error(ex, "Command {CommandId} open-folder failed.", commandId);
            PluginServices.ChatGui.PrintError($"Bazooka Lens open-folder failed: {ex.Message}");
        }
    }

    private async Task RunRestoreUiCommandAsync(long commandId)
    {
        try
        {
            PluginServices.Log.Information("Command {CommandId} restore-ui started.", commandId);
            await this.captureCoordinator.RestoreUiAsync().ConfigureAwait(false);
            PluginServices.ChatGui.Print("Bazooka Lens: restore-ui completed.");
            PluginServices.Log.Information("Command {CommandId} restore-ui completed.", commandId);
        }
        catch (Exception ex)
        {
            PluginServices.Log.Error(ex, "Command {CommandId} restore-ui failed.", commandId);
            PluginServices.ChatGui.PrintError($"Bazooka Lens restore-ui failed: {ex.Message}");
        }
    }

    private async Task RunRestoreDisplayCommandAsync(long commandId, RestoreDisplayOptions options)
    {
        try
        {
            PluginServices.Log.Information("Command {CommandId} restore-display started: {Options}", commandId, options);
            var status = await this.resizeProbeService.RestoreDisplayModeAsync(options, this.unloadToken).ConfigureAwait(false);
            PluginServices.ChatGui.Print("Bazooka Lens: restore-display completed; details written to /xllog.");
            PluginServices.Log.Information("Command {CommandId} restore-display result:\n{Status}", commandId, status);
        }
        catch (OperationCanceledException) when (this.unloadToken.IsCancellationRequested)
        {
            this.LogCommandCanceledForUnload(commandId, "restore-display");
        }
        catch (Exception ex)
        {
            PluginServices.Log.Error(ex, "Command {CommandId} restore-display failed.", commandId);
            PluginServices.ChatGui.PrintError($"Bazooka Lens restore-display failed: {ex.Message}");
        }
    }

    private void LogCommandCanceledForUnload(long commandId, string commandName)
    {
        PluginServices.Log.Information(
            "Command {CommandId} {CommandName} cancelled because Bazooka Lens is unloading.",
            commandId,
            commandName);
    }
}
