using BazookaLens.Capture;

namespace BazookaLens.Tests;

public sealed class CaptureRegionTests
{
    [Fact]
    public void FullCreatesViewportSizedRegion()
    {
        var region = CaptureRegion.Full(1920, 1080);

        Assert.Equal(0, region.X);
        Assert.Equal(0, region.Y);
        Assert.Equal(1920, region.Width);
        Assert.Equal(1080, region.Height);
    }

    [Fact]
    public void ToUvConvertsPixelsToNormalizedCoordinates()
    {
        var region = new CaptureRegion(100, 50, 400, 200);

        var (uv0, uv1) = region.ToUv(1000, 500);

        Assert.Equal(0.1f, uv0.X, 4);
        Assert.Equal(0.1f, uv0.Y, 4);
        Assert.Equal(0.5f, uv1.X, 4);
        Assert.Equal(0.5f, uv1.Y, 4);
    }

    [Fact]
    public void ScaleMultipliesCoordinatesAndDimensions()
    {
        var region = new CaptureRegion(10, 20, 300, 400);

        var scaled = region.Scale(1.5);

        Assert.Equal(new CaptureRegion(15, 30, 450, 600), scaled);
    }

    [Fact]
    public void ScaleRoundsToNearestPixel()
    {
        var region = new CaptureRegion(1, 3, 5, 7);

        var scaled = region.Scale(1.5);

        Assert.Equal(new CaptureRegion(2, 5, 8, 11), scaled);
    }

    [Theory]
    [InlineData(-1, 0, 10, 10)]
    [InlineData(0, -1, 10, 10)]
    [InlineData(0, 0, 0, 10)]
    [InlineData(0, 0, 10, 0)]
    [InlineData(900, 0, 200, 10)]
    [InlineData(0, 450, 10, 100)]
    public void ToUvRejectsRegionsOutsideViewport(int x, int y, int width, int height)
    {
        var region = new CaptureRegion(x, y, width, height);

        Assert.Throws<ArgumentOutOfRangeException>(() => region.ToUv(1000, 500));
    }

    [Theory]
    [InlineData(0, 1080)]
    [InlineData(1920, 0)]
    [InlineData(-1, 1080)]
    [InlineData(1920, -1)]
    public void FullRejectsInvalidViewportDimensions(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CaptureRegion.Full(width, height));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ScaleRejectsInvalidScale(double scale)
    {
        var region = new CaptureRegion(0, 0, 10, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => region.Scale(scale));
    }
}
