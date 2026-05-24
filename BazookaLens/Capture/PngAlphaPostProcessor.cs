using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace BazookaLens.Capture;

internal static class PngAlphaPostProcessor
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static bool TryForceOpaqueAlpha(
        byte[] png,
        [NotNullWhen(true)] out byte[]? rewrittenPng,
        out string? reason)
    {
        rewrittenPng = null;

        try
        {
            if (!TryDecodeRgba8Png(png, out var parsed, out reason))
                return false;

            for (var i = 3; i < parsed.Pixels.Length; i += 4)
                parsed.Pixels[i] = 255;

            var rawScanlines = EncodeFilterZeroScanlines(parsed.Width, parsed.Height, parsed.Pixels);
            var compressedImageData = Compress(rawScanlines);
            rewrittenPng = WritePng(parsed.Chunks, compressedImageData);
            reason = null;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or IOException or OverflowException)
        {
            reason = $"Could not rewrite PNG alpha: {ex.Message}";
            rewrittenPng = null;
            return false;
        }
    }

    public static bool TryForceOpaqueAlphaInPlace(string path, out string? reason)
    {
        byte[] png;
        try
        {
            png = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            reason = $"Could not read PNG file: {ex.Message}";
            return false;
        }

        if (!TryForceOpaqueAlpha(png, out var rewrittenPng, out reason))
            return false;

        var tempPath = path + ".opaque.tmp";
        try
        {
            File.WriteAllBytes(tempPath, rewrittenPng);
            File.Replace(tempPath, path, destinationBackupFileName: null);
            reason = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            reason = $"Could not rewrite PNG file: {ex.Message}";
            return false;
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    internal static bool TryDecodeRgba8ForTests(byte[] png, out byte[] pixels, out string? reason)
    {
        if (TryDecodeRgba8Png(png, out var parsed, out reason))
        {
            pixels = parsed.Pixels;
            return true;
        }

        pixels = [];
        return false;
    }

    internal static void WritePngForTests(
        Stream output,
        int width,
        int height,
        byte bitDepth,
        byte colorType,
        byte[] rawScanlines,
        IReadOnlyList<(string Type, byte[] Data)>? ancillaryChunks = null,
        int idatChunkCount = 1)
    {
        if (idatChunkCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(idatChunkCount), "IDAT chunk count must be positive.");

        output.Write(PngSignature);
        WriteChunk(output, "IHDR", CreateIhdr(width, height, bitDepth, colorType));

        if (ancillaryChunks is not null)
        {
            foreach (var chunk in ancillaryChunks)
                WriteChunk(output, chunk.Type, chunk.Data);
        }

        WriteSplitIdatChunks(output, Compress(rawScanlines), idatChunkCount);
        WriteChunk(output, "IEND", []);
    }

    private static bool TryDecodeRgba8Png(byte[] png, [NotNullWhen(true)] out ParsedRgbaPng? parsed, out string? reason)
    {
        parsed = null;

        if (png.Length < PngSignature.Length || !png.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature))
        {
            reason = "Input is not a PNG file.";
            return false;
        }

        if (!TryReadChunks(png, out var chunks, out reason))
            return false;

        var ihdrIndex = chunks.FindIndex(static chunk => chunk.Type == "IHDR");
        if (ihdrIndex < 0)
        {
            reason = "PNG does not contain an IHDR chunk.";
            return false;
        }

        var ihdr = chunks[ihdrIndex].Data;
        if (ihdr.Length != 13)
        {
            reason = $"PNG IHDR chunk has invalid length {ihdr.Length}.";
            return false;
        }

        var width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(ihdr.AsSpan(0, 4)));
        var height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(ihdr.AsSpan(4, 4)));
        var bitDepth = ihdr[8];
        var colorType = ihdr[9];
        var compressionMethod = ihdr[10];
        var filterMethod = ihdr[11];
        var interlaceMethod = ihdr[12];

        if (width <= 0 || height <= 0)
        {
            reason = $"PNG dimensions are invalid: {width}x{height}.";
            return false;
        }

        if (bitDepth != 8)
        {
            reason = $"Unsupported PNG bit depth {bitDepth}; only 8-bit RGBA PNGs are supported.";
            return false;
        }

        if (colorType != 6)
        {
            reason = $"Unsupported PNG color type {colorType}; only RGBA color type 6 can be made opaque.";
            return false;
        }

        if (compressionMethod != 0 || filterMethod != 0 || interlaceMethod != 0)
        {
            reason = $"Unsupported PNG encoding: compression={compressionMethod}, filter={filterMethod}, interlace={interlaceMethod}.";
            return false;
        }

        var idatData = CombineIdatChunks(chunks);
        if (idatData.Length == 0)
        {
            reason = "PNG does not contain image data.";
            return false;
        }

        var rowBytes = checked(width * 4);
        var expectedRawLength = checked(height * (rowBytes + 1));
        var rawScanlines = Decompress(idatData, expectedRawLength);
        if (rawScanlines.Length != expectedRawLength)
        {
            reason = $"PNG image data length mismatch: expected {expectedRawLength}, actual {rawScanlines.Length}.";
            return false;
        }

        if (!TryDecodeScanlines(width, height, rawScanlines, out var pixels, out reason))
            return false;

        parsed = new ParsedRgbaPng(width, height, chunks, pixels);
        reason = null;
        return true;
    }

    private static bool TryReadChunks(byte[] png, out List<PngChunk> chunks, out string? reason)
    {
        chunks = [];
        var offset = PngSignature.Length;

        while (offset < png.Length)
        {
            if (png.Length - offset < 12)
            {
                reason = "PNG chunk header is truncated.";
                return false;
            }

            var length = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset, 4));
            offset += 4;

            if (length > int.MaxValue)
            {
                reason = $"PNG chunk length is too large: {length}.";
                return false;
            }

            var typeBytes = png.AsSpan(offset, 4).ToArray();
            var type = Encoding.ASCII.GetString(typeBytes);
            offset += 4;

            var dataLength = (int)length;
            if (png.Length - offset < dataLength + 4)
            {
                reason = $"PNG chunk {type} is truncated.";
                return false;
            }

            var data = png.AsSpan(offset, dataLength).ToArray();
            offset += dataLength;

            var storedCrc = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset, 4));
            var computedCrc = ComputeCrc(typeBytes, data);
            if (storedCrc != computedCrc)
            {
                reason = $"PNG chunk {type} CRC mismatch.";
                return false;
            }

            offset += 4;

            chunks.Add(new PngChunk(type, data));
            if (type == "IEND")
            {
                reason = null;
                return true;
            }
        }

        reason = "PNG does not contain an IEND chunk.";
        return false;
    }

    private static byte[] CombineIdatChunks(List<PngChunk> chunks)
    {
        using var output = new MemoryStream();
        foreach (var chunk in chunks)
        {
            if (chunk.Type == "IDAT")
                output.Write(chunk.Data);
        }

        return output.ToArray();
    }

    private static byte[] Decompress(byte[] compressedData, int expectedLength)
    {
        using var input = new MemoryStream(compressedData);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream(expectedLength);
        zlib.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] Compress(byte[] rawData)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(rawData);
        }

        return output.ToArray();
    }

    private static bool TryDecodeScanlines(int width, int height, byte[] rawScanlines, out byte[] pixels, out string? reason)
    {
        const int BytesPerPixel = 4;
        var rowBytes = checked(width * BytesPerPixel);
        pixels = new byte[checked(width * height * BytesPerPixel)];
        var previousRow = new byte[rowBytes];
        var currentRow = new byte[rowBytes];
        var rawOffset = 0;
        var pixelOffset = 0;

        for (var y = 0; y < height; y++)
        {
            var filter = rawScanlines[rawOffset++];
            if (filter > 4)
            {
                reason = $"Unsupported PNG scanline filter {filter}.";
                pixels = [];
                return false;
            }

            for (var x = 0; x < rowBytes; x++)
            {
                var encoded = rawScanlines[rawOffset++];
                var left = x >= BytesPerPixel ? currentRow[x - BytesPerPixel] : 0;
                var up = previousRow[x];
                var upperLeft = x >= BytesPerPixel ? previousRow[x - BytesPerPixel] : 0;
                var predicted = filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upperLeft),
                    _ => 0,
                };

                currentRow[x] = unchecked((byte)(encoded + predicted));
            }

            Buffer.BlockCopy(currentRow, 0, pixels, pixelOffset, rowBytes);
            pixelOffset += rowBytes;

            (previousRow, currentRow) = (currentRow, previousRow);
        }

        reason = null;
        return true;
    }

    private static byte[] EncodeFilterZeroScanlines(int width, int height, byte[] pixels)
    {
        const int BytesPerPixel = 4;
        var rowBytes = checked(width * BytesPerPixel);
        var rawScanlines = new byte[checked(height * (rowBytes + 1))];

        for (var y = 0; y < height; y++)
        {
            var rawOffset = y * (rowBytes + 1);
            rawScanlines[rawOffset] = 0;
            Buffer.BlockCopy(pixels, y * rowBytes, rawScanlines, rawOffset + 1, rowBytes);
        }

        return rawScanlines;
    }

    private static byte[] WritePng(List<PngChunk> chunks, byte[] compressedImageData)
    {
        using var output = new MemoryStream();
        output.Write(PngSignature);

        var wroteIdat = false;
        foreach (var chunk in chunks)
        {
            if (chunk.Type == "IDAT")
            {
                if (!wroteIdat)
                {
                    WriteChunk(output, "IDAT", compressedImageData);
                    wroteIdat = true;
                }

                continue;
            }

            WriteChunk(output, chunk.Type, chunk.Data);
        }

        return output.ToArray();
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        Span<byte> scratch = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(scratch, checked((uint)data.Length));
        output.Write(scratch);

        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);

        var crc = ComputeCrc(typeBytes, data);
        BinaryPrimitives.WriteUInt32BigEndian(scratch, crc);
        output.Write(scratch);
    }

    private static void WriteSplitIdatChunks(Stream output, byte[] compressedImageData, int idatChunkCount)
    {
        var offset = 0;
        for (var i = 0; i < idatChunkCount; i++)
        {
            var remainingBytes = compressedImageData.Length - offset;
            var remainingChunks = idatChunkCount - i;
            var length = remainingBytes / remainingChunks;
            if (remainingBytes % remainingChunks != 0)
                length++;

            WriteChunk(output, "IDAT", compressedImageData.AsSpan(offset, length).ToArray());
            offset += length;
        }
    }

    private static byte[] CreateIhdr(int width, int height, byte bitDepth, byte colorType)
    {
        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0, 4), checked((uint)width));
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4, 4), checked((uint)height));
        ihdr[8] = bitDepth;
        ihdr[9] = colorType;
        return ihdr;
    }

    private static int Paeth(int left, int up, int upperLeft)
    {
        var p = left + up - upperLeft;
        var pa = Math.Abs(p - left);
        var pb = Math.Abs(p - up);
        var pc = Math.Abs(p - upperLeft);
        return pa <= pb && pa <= pc ? left : pb <= pc ? up : upperLeft;
    }

    private static uint ComputeCrc(byte[] type, byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        crc = UpdateCrc(crc, type);
        crc = UpdateCrc(crc, data);
        return crc ^ 0xFFFFFFFFu;
    }

    private static uint UpdateCrc(uint crc, byte[] data)
    {
        foreach (var value in data)
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);

        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < table.Length; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;

            table[n] = c;
        }

        return table;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record ParsedRgbaPng(int Width, int Height, List<PngChunk> Chunks, byte[] Pixels);

    private sealed record PngChunk(string Type, byte[] Data);
}
