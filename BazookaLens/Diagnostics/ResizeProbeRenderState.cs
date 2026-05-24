using System;
using System.Text;

using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;

namespace BazookaLens.Diagnostics;

internal sealed record ResizeProbeRenderState(
    DateTimeOffset TimestampUtc,
    nint DevicePtr,
    uint? DeviceWidth,
    uint? DeviceHeight,
    uint? DeviceNewWidth,
    uint? DeviceNewHeight,
    byte? DeviceRequestResolutionChange,
    nint DeviceHwnd,
    nint SwapChainPtr,
    uint? SwapChainWidth,
    uint? SwapChainHeight,
    nint DxgiSwapChainPtr,
    nint RenderTargetManagerPtr,
    uint? RenderResolutionWidth,
    uint? RenderResolutionHeight,
    ulong ImGuiViewportId,
    uint? ImGuiViewportWidth,
    uint? ImGuiViewportHeight)
{
    public static unsafe ResizeProbeRenderState Capture()
    {
        var device = Device.Instance();
        var rtm = RenderTargetManager.Instance();
        var viewport = ImGui.GetMainViewport();
        var viewportWidth = ToUIntDimension(viewport.Size.X);
        var viewportHeight = ToUIntDimension(viewport.Size.Y);

        if (device is null)
        {
            return new ResizeProbeRenderState(
                DateTimeOffset.UtcNow,
                nint.Zero,
                null,
                null,
                null,
                null,
                null,
                nint.Zero,
                nint.Zero,
                null,
                null,
                nint.Zero,
                (nint)rtm,
                rtm is null ? null : rtm->Resolution_Width,
                rtm is null ? null : rtm->Resolution_Height,
                viewport.ID,
                viewportWidth,
                viewportHeight);
        }

        var swapChain = device->SwapChain;
        return new ResizeProbeRenderState(
            DateTimeOffset.UtcNow,
            (nint)device,
            device->Width,
            device->Height,
            device->NewWidth,
            device->NewHeight,
            device->RequestResolutionChange,
            (nint)device->hWnd,
            (nint)swapChain,
            swapChain is null ? null : swapChain->Width,
            swapChain is null ? null : swapChain->Height,
            swapChain is null ? nint.Zero : (nint)swapChain->DXGISwapChain,
            (nint)rtm,
            rtm is null ? null : rtm->Resolution_Width,
            rtm is null ? null : rtm->Resolution_Height,
            viewport.ID,
            viewportWidth,
            viewportHeight);
    }

    public bool MatchesTarget(ResizeProbeTarget target)
    {
        return this.DeviceWidth == target.Width
            && this.DeviceHeight == target.Height
            && this.SwapChainWidth == target.Width
            && this.SwapChainHeight == target.Height
            && this.RenderResolutionWidth == target.Width
            && this.RenderResolutionHeight == target.Height
            && this.ImGuiViewportWidth == target.Width
            && this.ImGuiViewportHeight == target.Height;
    }

    public bool MatchesPresentationTarget(ResizeProbeTarget target)
    {
        return this.DeviceWidth == target.Width
            && this.DeviceHeight == target.Height
            && this.SwapChainWidth == target.Width
            && this.SwapChainHeight == target.Height
            && this.ImGuiViewportWidth == target.Width
            && this.ImGuiViewportHeight == target.Height;
    }

    public ResizeProbeTarget RequireDeviceTarget()
    {
        if (this.DeviceWidth is not { } width || this.DeviceHeight is not { } height)
            throw new InvalidOperationException("Current device dimensions are unavailable.");

        return new ResizeProbeTarget(width, height);
    }

    public string ToLogBlock(string label)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{label}:");
        sb.AppendLine($"  TimestampUtc={this.TimestampUtc:O}");
        sb.AppendLine($"  DevicePtr={FormatPtr(this.DevicePtr)}");
        sb.AppendLine($"  Device.Width={FormatUInt(this.DeviceWidth)}");
        sb.AppendLine($"  Device.Height={FormatUInt(this.DeviceHeight)}");
        sb.AppendLine($"  Device.NewWidth={FormatUInt(this.DeviceNewWidth)}");
        sb.AppendLine($"  Device.NewHeight={FormatUInt(this.DeviceNewHeight)}");
        sb.AppendLine($"  Device.RequestResolutionChange={this.DeviceRequestResolutionChange?.ToString() ?? "<unavailable>"}");
        sb.AppendLine($"  Device.hWnd={FormatPtr(this.DeviceHwnd)}");
        sb.AppendLine($"  SwapChainPtr={FormatPtr(this.SwapChainPtr)}");
        sb.AppendLine($"  SwapChain.Width={FormatUInt(this.SwapChainWidth)}");
        sb.AppendLine($"  SwapChain.Height={FormatUInt(this.SwapChainHeight)}");
        sb.AppendLine($"  SwapChain.DXGISwapChain={FormatPtr(this.DxgiSwapChainPtr)}");
        sb.AppendLine($"  RenderTargetManagerPtr={FormatPtr(this.RenderTargetManagerPtr)}");
        sb.AppendLine($"  RenderTargetManager.Resolution_Width={FormatUInt(this.RenderResolutionWidth)}");
        sb.AppendLine($"  RenderTargetManager.Resolution_Height={FormatUInt(this.RenderResolutionHeight)}");
        sb.AppendLine($"  ImGuiViewport.ID=0x{this.ImGuiViewportId:X}");
        sb.AppendLine($"  ImGuiViewport.Width={FormatUInt(this.ImGuiViewportWidth)}");
        sb.AppendLine($"  ImGuiViewport.Height={FormatUInt(this.ImGuiViewportHeight)}");
        return sb.ToString();
    }

    public string ToSummary()
    {
        return $"Device={FormatDimension(this.DeviceWidth, this.DeviceHeight)}, " +
            $"SwapChain={FormatDimension(this.SwapChainWidth, this.SwapChainHeight)}, " +
            $"Render={FormatDimension(this.RenderResolutionWidth, this.RenderResolutionHeight)}, " +
            $"Viewport={FormatDimension(this.ImGuiViewportWidth, this.ImGuiViewportHeight)}, " +
            $"RequestResolutionChange={this.DeviceRequestResolutionChange?.ToString() ?? "<unavailable>"}";
    }

    private static uint? ToUIntDimension(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
            return null;

        return (uint)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    private static string FormatUInt(uint? value)
    {
        return value?.ToString() ?? "<unavailable>";
    }

    private static string FormatDimension(uint? width, uint? height)
    {
        return width is { } w && height is { } h
            ? $"{w}x{h}"
            : "<unavailable>";
    }

    private static string FormatPtr(nint value)
    {
        return value == nint.Zero ? "null" : $"0x{value.ToInt64():X}";
    }
}
