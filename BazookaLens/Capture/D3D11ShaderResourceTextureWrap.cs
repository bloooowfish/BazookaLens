using System;
using System.Threading;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace BazookaLens.Capture;

internal sealed unsafe class D3D11ShaderResourceTextureWrap : IDalamudTextureWrap
{
    private nint shaderResourceView;

    public D3D11ShaderResourceTextureWrap(ID3D11ShaderResourceView* shaderResourceView, int width, int height, bool addRef)
    {
        if (shaderResourceView is null)
            throw new ArgumentNullException(nameof(shaderResourceView));

        this.shaderResourceView = (nint)shaderResourceView;
        this.Width = width;
        this.Height = height;

        if (addRef)
            ((IUnknown*)shaderResourceView)->AddRef();
    }

    ~D3D11ShaderResourceTextureWrap()
    {
        this.Dispose();
    }

    public ImTextureID Handle => new(this.shaderResourceView);

    public int Width { get; }

    public int Height { get; }

    public IDalamudTextureWrap CreateWrapSharingLowLevelResource()
    {
        var view = (ID3D11ShaderResourceView*)this.shaderResourceView;
        if (view is null)
            throw new ObjectDisposedException(nameof(D3D11ShaderResourceTextureWrap));

        return new D3D11ShaderResourceTextureWrap(view, this.Width, this.Height, addRef: true);
    }

    public void Dispose()
    {
        var view = Interlocked.Exchange(ref this.shaderResourceView, nint.Zero);
        if (view != nint.Zero)
            ((IUnknown*)view)->Release();

        GC.SuppressFinalize(this);
    }
}
