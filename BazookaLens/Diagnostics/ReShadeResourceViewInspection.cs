using System;

using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace BazookaLens.Diagnostics;

internal readonly record struct ReShadeResourceViewInspection(
    ulong ViewHandle,
    bool Succeeded,
    nint ResourcePointer,
    nint TexturePointer,
    string? Format,
    uint Width,
    uint Height,
    uint SampleCount,
    uint SampleQuality,
    string? Error)
{
    public static ReShadeResourceViewInspection Success(
        ulong viewHandle,
        nint resourcePointer,
        nint texturePointer,
        string format,
        uint width,
        uint height,
        uint sampleCount,
        uint sampleQuality)
    {
        return new ReShadeResourceViewInspection(
            viewHandle,
            Succeeded: true,
            resourcePointer,
            texturePointer,
            format,
            width,
            height,
            sampleCount,
            sampleQuality,
            Error: null);
    }

    public static ReShadeResourceViewInspection Failure(ulong viewHandle, string error)
    {
        return new ReShadeResourceViewInspection(
            viewHandle,
            Succeeded: false,
            ResourcePointer: nint.Zero,
            TexturePointer: nint.Zero,
            Format: null,
            Width: 0,
            Height: 0,
            SampleCount: 0,
            SampleQuality: 0,
            Error: error);
    }

    public override string ToString()
    {
        if (!this.Succeeded)
            return $"View={FormatPointer(this.ViewHandle)}, Error={this.Error ?? "<unknown>"}";

        return
            $"View={FormatPointer(this.ViewHandle)}, Resource={FormatPointer(this.ResourcePointer)}, Texture={FormatPointer(this.TexturePointer)}, " +
            $"Texture={this.Width}x{this.Height}, Format={this.Format ?? "<unknown>"}, SampleCount={this.SampleCount}, SampleQuality={this.SampleQuality}";
    }

    private static string FormatPointer(ulong pointer)
    {
        return $"0x{pointer:X}";
    }

    private static string FormatPointer(nint pointer)
    {
        return $"0x{pointer.ToInt64():X}";
    }
}

internal static unsafe class ReShadeD3D11ResourceViewInspector
{
    public static ReShadeResourceViewInspection Inspect(ulong viewHandle)
    {
        if (viewHandle == 0)
            return ReShadeResourceViewInspection.Failure(viewHandle, "resource view handle was zero");

        try
        {
            var view = (ID3D11View*)viewHandle;
            using var resource = default(ComPtr<ID3D11Resource>);
            view->GetResource(resource.GetAddressOf());
            if (resource.Get() is null)
                return ReShadeResourceViewInspection.Failure(viewHandle, "ID3D11View.GetResource returned null");

            using var texture = default(ComPtr<ID3D11Texture2D>);
            var hr = resource.As(&texture);
            if (hr.FAILED || texture.Get() is null)
                return ReShadeResourceViewInspection.Failure(viewHandle, $"resource is not ID3D11Texture2D: HRESULT=0x{hr.Value:X8}");

            D3D11_TEXTURE2D_DESC desc;
            texture.Get()->GetDesc(&desc);

            return ReShadeResourceViewInspection.Success(
                viewHandle,
                (nint)resource.Get(),
                (nint)texture.Get(),
                desc.Format.ToString(),
                desc.Width,
                desc.Height,
                desc.SampleDesc.Count,
                desc.SampleDesc.Quality);
        }
        catch (Exception ex)
        {
            return ReShadeResourceViewInspection.Failure(viewHandle, $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}
