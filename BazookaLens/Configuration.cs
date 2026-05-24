using System;
using BazookaLens.Capture;
using BazookaLens.UI;
using Dalamud.Configuration;

namespace BazookaLens;

[Serializable]
internal sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; }

    public double Scale { get; set; } = CaptureScalePolicy.DefaultScale;

    public bool RegionEnabled { get; set; }

    public CaptureRegion? Region { get; set; }

    public string? SaveDirectory { get; set; }

    public KeyboardShortcut? Shortcut { get; set; }

    public GuideMode GuideMode { get; set; } = GuideMode.RuleOfThirds;

    public int GridRows { get; set; } = GuideLayout.DefaultGridRows;

    public int GridColumns { get; set; } = GuideLayout.DefaultGridColumns;

    public void Sanitize()
    {
        this.Scale = CaptureScalePolicy.Normalize(this.Scale);
        if (!CaptureScalePolicy.IsValid(this.Scale, out _))
            this.Scale = CaptureScalePolicy.DefaultScale;

        this.GridRows = GuideLayout.ClampGridDivision(this.GridRows);
        this.GridColumns = GuideLayout.ClampGridDivision(this.GridColumns);
    }

    public void Save()
    {
        this.Sanitize();
        PluginServices.PluginInterface.SavePluginConfig(this);
    }
}
