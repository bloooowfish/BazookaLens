using System;
using System.Threading;
using System.Threading.Tasks;

using BazookaLens.Capture;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace BazookaLens.Diagnostics;

internal static class ReShadePostEffectsCaptureRegion
{
    public static CaptureRegion Resolve(CaptureRegion? region, uint textureWidth, uint textureHeight)
    {
        if (textureWidth > int.MaxValue || textureHeight > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(textureWidth), "Post-effects texture dimensions exceed supported capture bounds.");

        var resolved = region ?? CaptureRegion.Full((int)textureWidth, (int)textureHeight);
        _ = resolved.ToUv((int)textureWidth, (int)textureHeight);
        return resolved;
    }
}

internal readonly record struct ReShadePostEffectsCaptureTarget(uint Width, uint Height)
{
    public bool Matches(uint textureWidth, uint textureHeight)
    {
        return textureWidth == this.Width && textureHeight == this.Height;
    }

    public override string ToString()
    {
        return $"{this.Width}x{this.Height}";
    }
}

internal sealed class ReShadePostEffectsTextureSizeMismatchException : Exception
{
    public ReShadePostEffectsTextureSizeMismatchException(
        ReShadePostEffectsCaptureTarget expected,
        uint actualWidth,
        uint actualHeight)
        : base($"Post-effects texture size {actualWidth}x{actualHeight} did not match expected {expected}.")
    {
        this.Expected = expected;
        this.ActualWidth = actualWidth;
        this.ActualHeight = actualHeight;
    }

    public ReShadePostEffectsCaptureTarget Expected { get; }

    public uint ActualWidth { get; }

    public uint ActualHeight { get; }
}

internal sealed class ReShadePostEffectsCaptureRequest
{
    private readonly TaskCompletionSource<D3D11ShaderResourceTextureWrap> completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ReShadePostEffectsCaptureTimeoutDiagnostics timeoutDiagnostics = new();

    public ReShadePostEffectsCaptureRequest(CaptureRegion? region, ReShadePostEffectsCaptureTarget? expectedTexture)
    {
        this.Region = region;
        this.ExpectedTexture = expectedTexture;
    }

    public CaptureRegion? Region { get; }

    public ReShadePostEffectsCaptureTarget? ExpectedTexture { get; }

    public Task<D3D11ShaderResourceTextureWrap> Task => this.completion.Task;

    public long RecordTextureSizeMismatch(uint actualWidth, uint actualHeight)
    {
        return this.timeoutDiagnostics.RecordTextureSizeMismatch(actualWidth, actualHeight);
    }

    public bool TryComplete(D3D11ShaderResourceTextureWrap texture)
    {
        return this.completion.TrySetResult(texture);
    }

    public bool TrySetException(Exception exception)
    {
        return this.completion.TrySetException(exception);
    }

    public bool TryCancel()
    {
        return this.completion.TrySetCanceled();
    }

    public async Task<D3D11ShaderResourceTextureWrap> WaitAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action clearPending)
    {
        try
        {
            var timeoutTask = System.Threading.Tasks.Task.Delay(timeout, cancellationToken);
            var completed = await System.Threading.Tasks.Task.WhenAny(this.Task, timeoutTask).ConfigureAwait(false);
            if (completed == this.Task)
                return await this.Task.ConfigureAwait(false);

            clearPending();
            cancellationToken.ThrowIfCancellationRequested();
            this.TryCancel();
            throw new TimeoutException(this.BuildTimeoutMessage(timeout));
        }
        catch
        {
            clearPending();
            throw;
        }
    }

    private string BuildTimeoutMessage(TimeSpan timeout)
    {
        return this.timeoutDiagnostics.BuildTimeoutMessage(timeout, this.ExpectedTexture);
    }
}

internal sealed class ReShadePostEffectsCaptureTimeoutDiagnostics
{
    private long textureSizeMismatchCount;
    private long lastTextureSizeMismatchWidth;
    private long lastTextureSizeMismatchHeight;

    public long RecordTextureSizeMismatch(uint actualWidth, uint actualHeight)
    {
        Interlocked.Exchange(ref this.lastTextureSizeMismatchWidth, actualWidth);
        Interlocked.Exchange(ref this.lastTextureSizeMismatchHeight, actualHeight);
        return Interlocked.Increment(ref this.textureSizeMismatchCount);
    }

    public string BuildTimeoutMessage(TimeSpan timeout, ReShadePostEffectsCaptureTarget? expectedTexture)
    {
        var message = $"Timed out waiting for ReShadeFinishEffects post-effects capture after {timeout.TotalMilliseconds:0} ms.";
        var skippedCount = Interlocked.Read(ref this.textureSizeMismatchCount);
        if (skippedCount <= 0)
            return message;

        var lastWidth = Interlocked.Read(ref this.lastTextureSizeMismatchWidth);
        var lastHeight = Interlocked.Read(ref this.lastTextureSizeMismatchHeight);
        var frameLabel = skippedCount == 1 ? "frame" : "frames";
        return $"{message} Post-effects capture skipped {skippedCount} post-effects {frameLabel}; last mismatch={lastWidth}x{lastHeight}, expected={expectedTexture?.ToString() ?? "<any>"}.";
    }
}

internal readonly record struct ReShadePostEffectsCaptureSkip(
    uint ActualWidth,
    uint ActualHeight,
    string Reason);

internal static unsafe class ReShadePostEffectsTextureCopier
{
    public static ReShadePostEffectsTextureCopyResult CopyToShaderResourceTexture(
        ulong renderTargetViewHandle,
        CaptureRegion? requestedRegion,
        ReShadePostEffectsCaptureTarget? expectedTexture)
    {
        if (renderTargetViewHandle == 0)
            throw new InvalidOperationException("ReShade finish-effects render target view handle was zero.");

        var renderTargetView = (ID3D11RenderTargetView*)renderTargetViewHandle;
        D3D11_RENDER_TARGET_VIEW_DESC renderTargetViewDesc;
        renderTargetView->GetDesc(&renderTargetViewDesc);

        using var sourceResource = default(ComPtr<ID3D11Resource>);
        renderTargetView->GetResource(sourceResource.GetAddressOf());
        if (sourceResource.Get() is null)
            throw new InvalidOperationException("ReShade finish-effects render target view did not expose a resource.");

        using var sourceTexture = default(ComPtr<ID3D11Texture2D>);
        var hr = sourceResource.As(&sourceTexture);
        ThrowIfFailed(hr, "ReShade finish-effects resource is not an ID3D11Texture2D.");
        if (sourceTexture.Get() is null)
            throw new InvalidOperationException("ReShade finish-effects resource resolved to a null ID3D11Texture2D.");

        D3D11_TEXTURE2D_DESC sourceDesc;
        sourceTexture.Get()->GetDesc(&sourceDesc);
        if (expectedTexture is { } expected && !expected.Matches(sourceDesc.Width, sourceDesc.Height))
            throw new ReShadePostEffectsTextureSizeMismatchException(expected, sourceDesc.Width, sourceDesc.Height);

        if (sourceDesc.SampleDesc.Count > 1)
            throw new NotSupportedException($"Post-effects capture does not yet support multisampled render targets: SampleCount={sourceDesc.SampleDesc.Count}.");

        var region = ReShadePostEffectsCaptureRegion.Resolve(requestedRegion, sourceDesc.Width, sourceDesc.Height);
        var targetDesc = sourceDesc;
        targetDesc.Width = (uint)region.Width;
        targetDesc.Height = (uint)region.Height;
        if (renderTargetViewDesc.Format != DXGI_FORMAT.DXGI_FORMAT_UNKNOWN)
            targetDesc.Format = renderTargetViewDesc.Format;

        targetDesc.MipLevels = 1;
        targetDesc.ArraySize = 1;
        targetDesc.SampleDesc = new(1, 0);
        targetDesc.Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT;
        targetDesc.BindFlags = (uint)(D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE | D3D11_BIND_FLAG.D3D11_BIND_RENDER_TARGET);
        targetDesc.CPUAccessFlags = 0;
        targetDesc.MiscFlags = 0;

        using var device = default(ComPtr<ID3D11Device>);
        sourceTexture.Get()->GetDevice(device.GetAddressOf());
        if (device.Get() is null)
            throw new InvalidOperationException("Could not resolve D3D11 device from post-effects render target.");

        using var context = default(ComPtr<ID3D11DeviceContext>);
        device.Get()->GetImmediateContext(context.GetAddressOf());
        if (context.Get() is null)
            throw new InvalidOperationException("Could not resolve D3D11 immediate context from post-effects render target.");

        using var targetTexture = default(ComPtr<ID3D11Texture2D>);
        ThrowIfFailed(device.Get()->CreateTexture2D(&targetDesc, null, targetTexture.GetAddressOf()), "Failed to create post-effects capture texture.");

        var box = new D3D11_BOX
        {
            left = (uint)region.X,
            top = (uint)region.Y,
            front = 0,
            right = (uint)(region.X + region.Width),
            bottom = (uint)(region.Y + region.Height),
            back = 1,
        };

        context.Get()->CopySubresourceRegion(
            (ID3D11Resource*)targetTexture.Get(),
            0,
            0,
            0,
            0,
            (ID3D11Resource*)sourceTexture.Get(),
            0,
            &box);

        var srvDesc = new D3D11_SHADER_RESOURCE_VIEW_DESC(
            targetTexture,
            D3D_SRV_DIMENSION.D3D11_SRV_DIMENSION_TEXTURE2D);
        using var shaderResourceView = default(ComPtr<ID3D11ShaderResourceView>);
        ThrowIfFailed(
            device.Get()->CreateShaderResourceView(
                (ID3D11Resource*)targetTexture.Get(),
                &srvDesc,
                shaderResourceView.GetAddressOf()),
            "Failed to create post-effects capture shader resource view.");

        var texture = new D3D11ShaderResourceTextureWrap(
            shaderResourceView.Get(),
            checked((int)targetDesc.Width),
            checked((int)targetDesc.Height),
            addRef: true);
        var inspection = ReShadeResourceViewInspection.Success(
            renderTargetViewHandle,
            (nint)sourceResource.Get(),
            (nint)sourceTexture.Get(),
            targetDesc.Format.ToString(),
            sourceDesc.Width,
            sourceDesc.Height,
            sourceDesc.SampleDesc.Count,
            sourceDesc.SampleDesc.Quality);
        return new ReShadePostEffectsTextureCopyResult(texture, inspection);
    }

    private static void ThrowIfFailed(HRESULT hr, string message)
    {
        if (hr.FAILED)
            throw new InvalidOperationException($"{message} HRESULT=0x{hr.Value:X8}");
    }
}

internal readonly record struct ReShadePostEffectsTextureCopyResult(
    D3D11ShaderResourceTextureWrap Texture,
    ReShadeResourceViewInspection Inspection);
