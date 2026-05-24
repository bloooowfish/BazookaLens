using BazookaLens.Capture;

namespace BazookaLens.Tests;

public sealed class CaptureScalePolicyTests
{
    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(1.234, 1.23)]
    [InlineData(1.235, 1.24)]
    [InlineData(2.999, 3.0)]
    public void NormalizeRoundsToTwoDecimals(double input, double expected)
    {
        Assert.Equal(expected, CaptureScalePolicy.Normalize(input));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ValidateRejectsInvalidScale(double scale)
    {
        Assert.False(CaptureScalePolicy.IsValid(scale, out _));
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(1)]
    [InlineData(1.5)]
    [InlineData(4)]
    public void ValidateAcceptsValidScale(double scale)
    {
        Assert.True(CaptureScalePolicy.IsValid(scale, out _));
    }
}
