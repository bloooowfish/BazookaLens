using System.Globalization;

namespace BazookaLens.Capture;

internal static class CaptureScalePolicy
{
    public const double DefaultScale = 2.0;
    public const double MaxScale = 4.0;

    public static string ValidRangeError => FormatRangeError("Scale");

    public static string FormatRangeError(string subject)
    {
        return $"{subject} must be greater than 0 and no more than {MaxScale.ToString("0.00", CultureInfo.InvariantCulture)}.";
    }

    public static double Normalize(double scale)
    {
        return Math.Round(scale, 2, MidpointRounding.AwayFromZero);
    }

    public static bool IsValid(double scale, out string? error)
    {
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0 || scale > MaxScale)
        {
            error = ValidRangeError;
            return false;
        }

        error = null;
        return true;
    }

    public static double NormalizeOrThrow(double scale)
    {
        var normalized = Normalize(scale);
        if (!IsValid(normalized, out var error))
            throw new ArgumentOutOfRangeException(nameof(scale), error);

        return normalized;
    }
}
