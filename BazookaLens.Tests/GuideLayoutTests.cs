using BazookaLens.UI;

namespace BazookaLens.Tests;

public sealed class GuideLayoutTests
{
    [Fact]
    public void NoneCreatesNoGuideLines()
    {
        var layout = GuideLayout.Create(GuideMode.None);

        Assert.Empty(layout.Lines);
    }

    [Fact]
    public void RuleOfThirdsCreatesThirdSplitLines()
    {
        var layout = GuideLayout.Create(GuideMode.RuleOfThirds);

        Assert.Equal(
            new[]
            {
                new GuideLine(GuideLineOrientation.Vertical, 1f / 3f),
                new GuideLine(GuideLineOrientation.Vertical, 2f / 3f),
                new GuideLine(GuideLineOrientation.Horizontal, 1f / 3f),
                new GuideLine(GuideLineOrientation.Horizontal, 2f / 3f),
            },
            layout.Lines);
    }

    [Fact]
    public void CenterCrossCreatesHalfwayLines()
    {
        var layout = GuideLayout.Create(GuideMode.CenterCross);

        Assert.Equal(
            new[]
            {
                new GuideLine(GuideLineOrientation.Vertical, 0.5f),
                new GuideLine(GuideLineOrientation.Horizontal, 0.5f),
            },
            layout.Lines);
    }

    [Fact]
    public void GridCreatesOnlyInternalSplitLines()
    {
        var layout = GuideLayout.Create(GuideMode.Grid, columns: 4, rows: 3);

        Assert.Equal(
            new[]
            {
                new GuideLine(GuideLineOrientation.Vertical, 0.25f),
                new GuideLine(GuideLineOrientation.Vertical, 0.5f),
                new GuideLine(GuideLineOrientation.Vertical, 0.75f),
                new GuideLine(GuideLineOrientation.Horizontal, 1f / 3f),
                new GuideLine(GuideLineOrientation.Horizontal, 2f / 3f),
            },
            layout.Lines);
    }

    [Fact]
    public void GridClampsRowsAndColumnsToSupportedRange()
    {
        var tooSmall = GuideLayout.Create(GuideMode.Grid, columns: -1, rows: 0);
        var tooLarge = GuideLayout.Create(GuideMode.Grid, columns: 100, rows: 100);

        Assert.Empty(tooSmall.Lines);
        Assert.Equal(46, tooLarge.Lines.Count);
        Assert.Equal(new GuideLine(GuideLineOrientation.Vertical, 1f / 24f), tooLarge.Lines[0]);
        Assert.Equal(new GuideLine(GuideLineOrientation.Horizontal, 23f / 24f), tooLarge.Lines[^1]);
    }

    [Fact]
    public void GoldenCreatesGoldenRatioSplitLines()
    {
        var layout = GuideLayout.Create(GuideMode.Golden);

        Assert.Equal(
            new[]
            {
                new GuideLine(GuideLineOrientation.Vertical, 0.382f),
                new GuideLine(GuideLineOrientation.Vertical, 0.618f),
                new GuideLine(GuideLineOrientation.Horizontal, 0.382f),
                new GuideLine(GuideLineOrientation.Horizontal, 0.618f),
            },
            layout.Lines);
    }

    [Theory]
    [InlineData((int)GuideLineOrientation.Vertical, 0f)]
    [InlineData((int)GuideLineOrientation.Vertical, 1f)]
    [InlineData((int)GuideLineOrientation.Horizontal, -0.1f)]
    [InlineData((int)GuideLineOrientation.Horizontal, 1.1f)]
    [InlineData((int)GuideLineOrientation.Horizontal, float.NaN)]
    public void GuideLineRejectsInvalidNormalizedPosition(int orientation, float normalizedPosition)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GuideLine((GuideLineOrientation)orientation, normalizedPosition));
    }
}
