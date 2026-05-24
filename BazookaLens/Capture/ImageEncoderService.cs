using System;

namespace BazookaLens.Capture;

internal sealed class ImageEncoderService
{
    private static readonly Guid PngContainerGuid = new("1B7CFAF4-713F-473C-BBCD-6137425FAEAF");

    public Guid GetPngContainerGuid()
    {
        return PngContainerGuid;
    }
}
