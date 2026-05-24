using System;
using System.Numerics;

namespace BazookaLens.Capture;

internal readonly record struct CaptureRegion(int X, int Y, int Width, int Height)
{
    public static CaptureRegion Full(int viewportWidth, int viewportHeight)
    {
        if (viewportWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewportWidth), "Viewport width must be positive.");

        if (viewportHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewportHeight), "Viewport height must be positive.");

        return new CaptureRegion(0, 0, viewportWidth, viewportHeight);
    }

    public (Vector2 Uv0, Vector2 Uv1) ToUv(int viewportWidth, int viewportHeight)
    {
        if (viewportWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewportWidth), "Viewport width must be positive.");

        if (viewportHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewportHeight), "Viewport height must be positive.");

        this.ValidateInside(viewportWidth, viewportHeight);

        var uv0 = new Vector2((float)this.X / viewportWidth, (float)this.Y / viewportHeight);
        var uv1 = new Vector2((float)(this.X + this.Width) / viewportWidth, (float)(this.Y + this.Height) / viewportHeight);
        return (uv0, uv1);
    }

    public CaptureRegion Scale(double scale)
    {
        if (scale <= 0 || double.IsNaN(scale) || double.IsInfinity(scale))
            throw new ArgumentOutOfRangeException(nameof(scale), "Region scale must be positive and finite.");

        return new CaptureRegion(
            ScalePixel(this.X, scale),
            ScalePixel(this.Y, scale),
            ScalePixel(this.Width, scale),
            ScalePixel(this.Height, scale));
    }

    private static int ScalePixel(int value, double scale)
    {
        var scaled = Math.Round(value * scale, MidpointRounding.AwayFromZero);
        if (scaled > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(scale), "Scaled region exceeds the supported pixel range.");

        return (int)scaled;
    }

    private void ValidateInside(int viewportWidth, int viewportHeight)
    {
        if (this.X < 0)
            throw new ArgumentOutOfRangeException(nameof(this.X), "Region X must be non-negative.");

        if (this.Y < 0)
            throw new ArgumentOutOfRangeException(nameof(this.Y), "Region Y must be non-negative.");

        if (this.Width <= 0)
            throw new ArgumentOutOfRangeException(nameof(this.Width), "Region width must be positive.");

        if (this.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(this.Height), "Region height must be positive.");

        if ((long)this.X + this.Width > viewportWidth)
            throw new ArgumentOutOfRangeException(nameof(this.Width), "Region extends beyond viewport width.");

        if ((long)this.Y + this.Height > viewportHeight)
            throw new ArgumentOutOfRangeException(nameof(this.Height), "Region extends beyond viewport height.");
    }
}
