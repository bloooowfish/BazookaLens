using System;

namespace BazookaLens.Capture;

internal sealed record ImageEncoderInfo(
    CaptureImageFormat Format,
    Guid ContainerGuid,
    string FileExtension,
    string DisplayName);

internal sealed class ImageEncoderService
{
    private static readonly Guid PngContainerGuid = new("1B7CFAF4-713F-473C-BBCD-6137425FAEAF");
    private static readonly Guid BmpContainerGuid = new("0AF1D87E-FCFE-4188-BDEB-A7906471CBE3");
    private readonly Func<CaptureImageFormat> formatProvider;

    public ImageEncoderService(Func<CaptureImageFormat>? formatProvider = null)
    {
        this.formatProvider = formatProvider ?? (() => CaptureImageFormat.Png);
    }

    public static ImageEncoderInfo GetEncoder(CaptureImageFormat format)
    {
        return format switch
        {
            CaptureImageFormat.Png => new ImageEncoderInfo(format, PngContainerGuid, ".png", "PNG"),
            CaptureImageFormat.Bmp => new ImageEncoderInfo(format, BmpContainerGuid, ".bmp", "BMP"),
            _ => GetEncoder(CaptureImageFormat.Png),
        };
    }

    public ImageEncoderInfo GetCurrentEncoder()
    {
        return GetEncoder(this.formatProvider());
    }
}
