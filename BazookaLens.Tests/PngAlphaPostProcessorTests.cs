using BazookaLens.Capture;
using System.Text;

namespace BazookaLens.Tests;

public sealed class PngAlphaPostProcessorTests
{
    [Fact]
    public void TryForceOpaqueAlphaPreservesRgbAndSetsAlphaForRgba8Png()
    {
        var png = PngTestImage.CreateRgba8(
            width: 2,
            height: 1,
            pixels:
            [
                10, 20, 30, 0,
                40, 50, 60, 128,
            ]);

        Assert.True(PngAlphaPostProcessor.TryForceOpaqueAlpha(png, out var rewritten, out var reason));
        Assert.Null(reason);

        var pixels = PngTestImage.DecodeRgba8(rewritten);
        Assert.Equal(
            [10, 20, 30, 255, 40, 50, 60, 255],
            pixels);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void TryForceOpaqueAlphaSupportsPngScanlineFilters(byte filter)
    {
        byte[] pixels =
        [
            10, 20, 30, 0,
            40, 50, 60, 1,
            70, 80, 90, 2,
            100, 110, 120, 3,
            130, 140, 150, 4,
            160, 170, 180, 5,
        ];
        var png = PngTestImage.CreateRgba8WithFilters(
            width: 3,
            height: 2,
            pixels,
            [filter, filter]);

        Assert.True(PngAlphaPostProcessor.TryForceOpaqueAlpha(png, out var rewritten, out var reason));
        Assert.Null(reason);

        var expected = pixels.ToArray();
        for (var i = 3; i < expected.Length; i += 4)
            expected[i] = 255;

        Assert.Equal(expected, PngTestImage.DecodeRgba8(rewritten));
    }

    [Fact]
    public void TryForceOpaqueAlphaCombinesMultipleIdatChunksAndPreservesAncillaryChunks()
    {
        var png = PngTestImage.CreateRgba8(
            width: 2,
            height: 1,
            pixels:
            [
                10, 20, 30, 0,
                40, 50, 60, 0,
            ],
            ancillaryChunks: [("tEXt", Encoding.ASCII.GetBytes("Comment\0Bazooka Lens"))],
            idatChunkCount: 3);

        Assert.True(PngAlphaPostProcessor.TryForceOpaqueAlpha(png, out var rewritten, out var reason));
        Assert.Null(reason);

        Assert.Equal([10, 20, 30, 255, 40, 50, 60, 255], PngTestImage.DecodeRgba8(rewritten));
        Assert.True(PngTestImage.ContainsChunk(rewritten, "tEXt"));
    }

    [Fact]
    public void TryForceOpaqueAlphaRejectsRgbPngWithoutAlpha()
    {
        var png = PngTestImage.CreateRgb8(width: 1, height: 1, pixels: [10, 20, 30]);

        Assert.False(PngAlphaPostProcessor.TryForceOpaqueAlpha(png, out var rewritten, out var reason));
        Assert.Null(rewritten);
        Assert.Contains("color type", reason);
    }

    [Fact]
    public void TryForceOpaqueAlphaInPlaceLeavesOriginalFileWhenPngIsUnsupported()
    {
        var png = PngTestImage.CreateRgb8(width: 1, height: 1, pixels: [10, 20, 30]);
        var path = Path.Combine(Path.GetTempPath(), $"bazooka-lens-png-alpha-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, png);

        try
        {
            Assert.False(PngAlphaPostProcessor.TryForceOpaqueAlphaInPlace(path, out var reason));
            Assert.Contains("color type", reason);
            Assert.Equal(png, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static class PngTestImage
    {
        public static byte[] CreateRgba8(
            int width,
            int height,
            byte[] pixels,
            IReadOnlyList<(string Type, byte[] Data)>? ancillaryChunks = null,
            int idatChunkCount = 1)
        {
            return Create(
                width,
                height,
                colorType: 6,
                bytesPerPixel: 4,
                pixels,
                ancillaryChunks,
                idatChunkCount);
        }

        public static byte[] CreateRgb8(int width, int height, byte[] pixels)
        {
            return Create(
                width,
                height,
                colorType: 2,
                bytesPerPixel: 3,
                pixels,
                ancillaryChunks: null,
                idatChunkCount: 1);
        }

        public static byte[] CreateRgba8WithFilters(int width, int height, byte[] pixels, byte[] filters)
        {
            var rawScanlines = EncodeFilteredScanlines(width, height, bytesPerPixel: 4, pixels, filters);
            using var output = new MemoryStream();
            PngAlphaPostProcessor.WritePngForTests(output, width, height, bitDepth: 8, colorType: 6, rawScanlines);
            return output.ToArray();
        }

        public static byte[] DecodeRgba8(byte[] png)
        {
            Assert.True(PngAlphaPostProcessor.TryDecodeRgba8ForTests(png, out var pixels, out var reason));
            Assert.Null(reason);
            return pixels;
        }

        public static bool ContainsChunk(byte[] png, string chunkType)
        {
            var chunkTypeBytes = Encoding.ASCII.GetBytes(chunkType);
            for (var i = 8; i <= png.Length - chunkTypeBytes.Length; i++)
            {
                if (png.AsSpan(i, chunkTypeBytes.Length).SequenceEqual(chunkTypeBytes))
                    return true;
            }

            return false;
        }

        private static byte[] Create(
            int width,
            int height,
            byte colorType,
            int bytesPerPixel,
            byte[] pixels,
            IReadOnlyList<(string Type, byte[] Data)>? ancillaryChunks,
            int idatChunkCount)
        {
            var expected = checked(width * height * bytesPerPixel);
            Assert.Equal(expected, pixels.Length);
            var rawScanlines = EncodeFilteredScanlines(
                width,
                height,
                bytesPerPixel,
                pixels,
                Enumerable.Repeat((byte)0, height).ToArray());

            using var output = new MemoryStream();
            PngAlphaPostProcessor.WritePngForTests(
                output,
                width,
                height,
                bitDepth: 8,
                colorType,
                rawScanlines,
                ancillaryChunks,
                idatChunkCount);
            return output.ToArray();
        }

        private static byte[] EncodeFilteredScanlines(
            int width,
            int height,
            int bytesPerPixel,
            byte[] pixels,
            byte[] filters)
        {
            Assert.Equal(height, filters.Length);
            using var raw = new MemoryStream();
            for (var y = 0; y < height; y++)
            {
                var filter = filters[y];
                raw.WriteByte(filter);

                var rowOffset = y * width * bytesPerPixel;
                var rowLength = width * bytesPerPixel;
                for (var x = 0; x < rowLength; x++)
                {
                    var value = pixels[rowOffset + x];
                    var left = x >= bytesPerPixel ? pixels[rowOffset + x - bytesPerPixel] : 0;
                    var up = y > 0 ? pixels[rowOffset - rowLength + x] : 0;
                    var upperLeft = y > 0 && x >= bytesPerPixel ? pixels[rowOffset - rowLength + x - bytesPerPixel] : 0;
                    var predicted = filter switch
                    {
                        0 => 0,
                        1 => left,
                        2 => up,
                        3 => (left + up) / 2,
                        4 => Paeth(left, up, upperLeft),
                        _ => throw new ArgumentOutOfRangeException(nameof(filters), filter, "Unsupported PNG filter."),
                    };

                    raw.WriteByte(unchecked((byte)(value - predicted)));
                }
            }

            return raw.ToArray();
        }

        private static int Paeth(int left, int up, int upperLeft)
        {
            var p = left + up - upperLeft;
            var pa = Math.Abs(p - left);
            var pb = Math.Abs(p - up);
            var pc = Math.Abs(p - upperLeft);
            return pa <= pb && pa <= pc ? left : pb <= pc ? up : upperLeft;
        }
    }
}
