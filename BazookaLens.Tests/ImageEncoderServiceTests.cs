using BazookaLens.Capture;

namespace BazookaLens.Tests;

public sealed class ImageEncoderServiceTests
{
    [Theory]
    [InlineData(CaptureImageFormat.Png, ".png", "1b7cfaf4-713f-473c-bbcd-6137425faeaf")]
    [InlineData(CaptureImageFormat.Bmp, ".bmp", "0af1d87e-fcfe-4188-bdeb-a7906471cbe3")]
    internal void GetsEncoderForSupportedFormat(CaptureImageFormat format, string extension, string containerGuid)
    {
        var encoder = ImageEncoderService.GetEncoder(format);

        Assert.Equal(format, encoder.Format);
        Assert.Equal(extension, encoder.FileExtension);
        Assert.Equal(Guid.Parse(containerGuid), encoder.ContainerGuid);
    }
}
