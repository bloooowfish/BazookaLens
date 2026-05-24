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
}
