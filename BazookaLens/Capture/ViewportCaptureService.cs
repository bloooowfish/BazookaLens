using System;
using System.Threading;
using System.Threading.Tasks;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;

namespace BazookaLens.Capture;

internal sealed class ViewportCaptureService
{
    private sealed record PreparedViewportCapture(
        ImGuiViewportTextureArgs Args,
        string OutputPath,
        Guid PngGuid,
        int ViewportWidth,
        int ViewportHeight,
        CaptureRegion Region);

    private readonly ImageEncoderService encoderService;
    private readonly CapturePathService pathService;

    public ViewportCaptureService(ImageEncoderService encoderService, CapturePathService pathService)
    {
        this.encoderService = encoderService;
        this.pathService = pathService;
    }

    public async Task ValidateOptionsAsync(CaptureOptions options, CancellationToken cancellationToken = default)
    {
        if (options.Region is null)
            return;

        cancellationToken.ThrowIfCancellationRequested();
        await PluginServices.Framework
            .RunOnFrameworkThread(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var viewport = ImGui.GetMainViewport();
                    var viewportWidth = checked((int)MathF.Round(viewport.Size.X));
                    var viewportHeight = checked((int)MathF.Round(viewport.Size.Y));
                    _ = options.Region.Value.ToUv(viewportWidth, viewportHeight);

                    PluginServices.Log.Information(
                        "Capture region preflight validated: Viewport={ViewportWidth}x{ViewportHeight}, Region={Region}",
                        viewportWidth,
                        viewportHeight,
                        options.Region.Value);
                })
            .ConfigureAwait(false);
    }

    public async Task<string> CaptureAsync(CaptureOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PluginServices.Log.Information(
            "Viewport capture requested: Timing={Timing}, HideGameUi={HideGameUi}, Region={Region}",
            options.Timing,
            options.HideGameUi,
            options.Region?.ToString() ?? "<full>");

        var prepared = await PluginServices.Framework
            .RunOnFrameworkThread(() => this.PrepareCapture(options))
            .ConfigureAwait(false);

        PluginServices.Log.Information(
            "Viewport capture prepared: ViewportId=0x{ViewportId:X}, Viewport={ViewportWidth}x{ViewportHeight}, Region={Region}, Uv0={Uv0}, Uv1={Uv1}, OutputPath={OutputPath}",
            prepared.Args.ViewportId,
            prepared.ViewportWidth,
            prepared.ViewportHeight,
            prepared.Region,
            prepared.Args.Uv0,
            prepared.Args.Uv1,
            prepared.OutputPath);

        var textureTask = await PluginServices.Framework
            .RunOnFrameworkThread<Task<IDalamudTextureWrap>>(
                () => PluginServices.TextureProvider.CreateFromImGuiViewportAsync(
                    prepared.Args,
                    "Bazooka Lens capture",
                    cancellationToken))
            .ConfigureAwait(false);

        using var texture = await textureTask.ConfigureAwait(false);
        PluginServices.Log.Information(
            "Viewport texture captured: Texture={Width}x{Height}, OutputPath={OutputPath}",
            texture.Width,
            texture.Height,
            prepared.OutputPath);

        await PluginServices.TextureReadbackProvider
            .SaveToFileAsync(texture, prepared.PngGuid, prepared.OutputPath, leaveWrapOpen: true, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        PluginServices.Log.Information("Viewport capture saved: {OutputPath}", prepared.OutputPath);
        return prepared.OutputPath;
    }

    public async Task<string> SaveTextureAsync(
        IDalamudTextureWrap texture,
        CaptureOptions options,
        string captureSource,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var outputPath = this.pathService.CreateOutputPath(options.Region);
        var pngGuid = this.encoderService.GetPngContainerGuid();
        var forceOpaqueAlpha = TextureCaptureSavePolicy.ShouldMakeOpaque(captureSource);

        PluginServices.Log.Information(
            "Texture capture save requested: Source={CaptureSource}, Texture={Width}x{Height}, Region={Region}, ForceOpaqueAlpha={ForceOpaqueAlpha}, OutputPath={OutputPath}",
            captureSource,
            texture.Width,
            texture.Height,
            options.Region?.ToString() ?? "<full>",
            forceOpaqueAlpha,
            outputPath);

        await PluginServices.TextureReadbackProvider
            .SaveToFileAsync(texture, pngGuid, outputPath, leaveWrapOpen: true, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (forceOpaqueAlpha)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (PngAlphaPostProcessor.TryForceOpaqueAlphaInPlace(outputPath, out var reason))
            {
                PluginServices.Log.Information(
                    "Texture capture PNG alpha forced opaque: Source={CaptureSource}, OutputPath={OutputPath}",
                    captureSource,
                    outputPath);
            }
            else
            {
                PluginServices.Log.Warning(
                    "Texture capture PNG alpha opaque post-process skipped: Source={CaptureSource}, OutputPath={OutputPath}, Reason={Reason}",
                    captureSource,
                    outputPath,
                    reason ?? "<none>");
            }
        }

        PluginServices.Log.Information("Texture capture saved: Source={CaptureSource}, OutputPath={OutputPath}", captureSource, outputPath);
        return outputPath;
    }

    private PreparedViewportCapture PrepareCapture(CaptureOptions options)
    {
        var viewport = ImGui.GetMainViewport();
        var viewportWidth = checked((int)MathF.Round(viewport.Size.X));
        var viewportHeight = checked((int)MathF.Round(viewport.Size.Y));
        var region = options.Region ?? CaptureRegion.Full(viewportWidth, viewportHeight);
        var (uv0, uv1) = region.ToUv(viewportWidth, viewportHeight);

        var args = new ImGuiViewportTextureArgs
        {
            ViewportId = viewport.ID,
            TakeBeforeImGuiRender = options.Timing == CaptureTiming.BeforeImGui,
            Uv0 = uv0,
            Uv1 = uv1,
        };

        var outputPath = this.pathService.CreateOutputPath(options.Region);
        var pngGuid = this.encoderService.GetPngContainerGuid();

        return new PreparedViewportCapture(args, outputPath, pngGuid, viewportWidth, viewportHeight, region);
    }
}
