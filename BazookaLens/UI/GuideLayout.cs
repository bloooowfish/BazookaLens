using System;
using System.Collections.Generic;

namespace BazookaLens.UI;

internal sealed class GuideLayout
{
    public const int DefaultGridRows = 3;
    public const int DefaultGridColumns = 3;
    public const int MinGridDivisions = 1;
    public const int MaxGridDivisions = 24;

    private GuideLayout(IReadOnlyList<GuideLine> lines)
    {
        this.Lines = lines;
    }

    public IReadOnlyList<GuideLine> Lines { get; }

    public static GuideLayout Create(GuideMode mode, int columns = DefaultGridColumns, int rows = DefaultGridRows)
    {
        return mode switch
        {
            GuideMode.None => new GuideLayout(Array.Empty<GuideLine>()),
            GuideMode.RuleOfThirds => new GuideLayout(
            [
                new GuideLine(GuideLineOrientation.Vertical, 1f / 3f),
                new GuideLine(GuideLineOrientation.Vertical, 2f / 3f),
                new GuideLine(GuideLineOrientation.Horizontal, 1f / 3f),
                new GuideLine(GuideLineOrientation.Horizontal, 2f / 3f),
            ]),
            GuideMode.CenterCross => new GuideLayout(
            [
                new GuideLine(GuideLineOrientation.Vertical, 0.5f),
                new GuideLine(GuideLineOrientation.Horizontal, 0.5f),
            ]),
            GuideMode.Grid => CreateGrid(columns, rows),
            GuideMode.Golden => new GuideLayout(
            [
                new GuideLine(GuideLineOrientation.Vertical, 0.382f),
                new GuideLine(GuideLineOrientation.Vertical, 0.618f),
                new GuideLine(GuideLineOrientation.Horizontal, 0.382f),
                new GuideLine(GuideLineOrientation.Horizontal, 0.618f),
            ]),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown guide mode."),
        };
    }

    private static GuideLayout CreateGrid(int columns, int rows)
    {
        columns = ClampGridDivision(columns);
        rows = ClampGridDivision(rows);

        var lines = new List<GuideLine>((columns - 1) + (rows - 1));
        for (var column = 1; column < columns; column++)
            lines.Add(new GuideLine(GuideLineOrientation.Vertical, (float)column / columns));

        for (var row = 1; row < rows; row++)
            lines.Add(new GuideLine(GuideLineOrientation.Horizontal, (float)row / rows));

        return new GuideLayout(lines);
    }

    public static int ClampGridDivision(int value)
    {
        return Math.Clamp(value, MinGridDivisions, MaxGridDivisions);
    }
}
