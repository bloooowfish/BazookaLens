using System;
using System.Text;

using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;

namespace BazookaLens.Diagnostics;

internal sealed record RenderResolutionSnapshot(
    string DevicePtr,
    string DeviceWidth,
    string DeviceHeight,
    string DeviceNewWidth,
    string DeviceNewHeight,
    string DeviceRequestResolutionChange,
    string DeviceHwnd,
    string SwapChainPtr,
    string SwapChainWidth,
    string SwapChainHeight,
    string DxgiSwapChainPtr,
    string RenderTargetManagerPtr,
    string RenderResolutionWidth,
    string RenderResolutionHeight,
    string ImGuiViewportId,
    string ImGuiViewportWidth,
    string ImGuiViewportHeight)
{
    public static unsafe RenderResolutionSnapshot Capture()
    {
        var device = Device.Instance();
        var rtm = RenderTargetManager.Instance();
        var viewport = ImGui.GetMainViewport();

        if (device is null)
        {
            return new RenderResolutionSnapshot(
                Ptr(nint.Zero),
                "<device-null>",
                "<device-null>",
                "<device-null>",
                "<device-null>",
                "<device-null>",
                "null",
                Ptr(nint.Zero),
                "<device-null>",
                "<device-null>",
                Ptr(nint.Zero),
                Ptr((nint)rtm),
                rtm is null ? "<rtm-null>" : rtm->Resolution_Width.ToString(),
                rtm is null ? "<rtm-null>" : rtm->Resolution_Height.ToString(),
                $"0x{viewport.ID:X}",
                viewport.Size.X.ToString("F0"),
                viewport.Size.Y.ToString("F0"));
        }

        var swapChain = device->SwapChain;
        return new RenderResolutionSnapshot(
            Ptr((nint)device),
            device->Width.ToString(),
            device->Height.ToString(),
            device->NewWidth.ToString(),
            device->NewHeight.ToString(),
            device->RequestResolutionChange.ToString(),
            Ptr((nint)device->hWnd),
            Ptr((nint)swapChain),
            swapChain is null ? "<swapchain-null>" : swapChain->Width.ToString(),
            swapChain is null ? "<swapchain-null>" : swapChain->Height.ToString(),
            swapChain is null ? Ptr(nint.Zero) : Ptr((nint)swapChain->DXGISwapChain),
            Ptr((nint)rtm),
            rtm is null ? "<rtm-null>" : rtm->Resolution_Width.ToString(),
            rtm is null ? "<rtm-null>" : rtm->Resolution_Height.ToString(),
            $"0x{viewport.ID:X}",
            viewport.Size.X.ToString("F0"),
            viewport.Size.Y.ToString("F0"));
    }

    public string ToLogBlock()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Render:");
        sb.AppendLine($"  DevicePtr={this.DevicePtr}");
        sb.AppendLine($"  Device.Width={this.DeviceWidth}");
        sb.AppendLine($"  Device.Height={this.DeviceHeight}");
        sb.AppendLine($"  Device.NewWidth={this.DeviceNewWidth}");
        sb.AppendLine($"  Device.NewHeight={this.DeviceNewHeight}");
        sb.AppendLine($"  Device.RequestResolutionChange={this.DeviceRequestResolutionChange}");
        sb.AppendLine($"  Device.hWnd={this.DeviceHwnd}");
        sb.AppendLine($"  SwapChainPtr={this.SwapChainPtr}");
        sb.AppendLine($"  SwapChain.Width={this.SwapChainWidth}");
        sb.AppendLine($"  SwapChain.Height={this.SwapChainHeight}");
        sb.AppendLine($"  SwapChain.DXGISwapChain={this.DxgiSwapChainPtr}");
        sb.AppendLine($"  RenderTargetManagerPtr={this.RenderTargetManagerPtr}");
        sb.AppendLine($"  RenderTargetManager.Resolution_Width={this.RenderResolutionWidth}");
        sb.AppendLine($"  RenderTargetManager.Resolution_Height={this.RenderResolutionHeight}");
        sb.AppendLine($"  ImGuiViewport.ID={this.ImGuiViewportId}");
        sb.AppendLine($"  ImGuiViewport.Width={this.ImGuiViewportWidth}");
        sb.AppendLine($"  ImGuiViewport.Height={this.ImGuiViewportHeight}");
        return sb.ToString();
    }

    private static string Ptr(nint value)
    {
        return value == nint.Zero ? "null" : $"0x{value.ToInt64():X}";
    }
}
