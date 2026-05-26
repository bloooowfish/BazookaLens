using BazookaLens.Capture;

namespace BazookaLens.Tests;

public sealed class CapturePathServiceTests
{
    [Fact]
    public void ResolveScreenshotDirectoryUsesConfiguredPathWhenPresent()
    {
        var resolved = CapturePathService.ResolveScreenshotDirectory(
            defaultDirectory: @"C:\default",
            configuredDirectory: @"D:\shots");

        Assert.Equal(@"D:\shots", resolved);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveScreenshotDirectoryFallsBackToDefault(string? configured)
    {
        var resolved = CapturePathService.ResolveScreenshotDirectory(@"C:\default", configured);

        Assert.Equal(@"C:\default", resolved);
    }

    [Theory]
    [InlineData(".png", "bazooka-lens-20260527-010203-456-0007-full.png")]
    [InlineData(".bmp", "bazooka-lens-20260527-010203-456-0007-full.bmp")]
    public void CreateFileNameUsesRequestedExtension(string extension, string expected)
    {
        var fileName = CapturePathService.CreateFileName(
            "20260527-010203-456",
            7,
            region: null,
            extension);

        Assert.Equal(expected, fileName);
    }
}
