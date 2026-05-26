namespace BazookaLens.Tests;

public sealed class RegionUiTests
{
    [Fact]
    public void RegionControlsUseCheckboxInsteadOfModeButtons()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("ImGui.Checkbox(\"Use Region", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ImGui.Button(\"Full Frame", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ImGui.Button(\"Use Region", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RegionControlsDimInactiveEditableArea()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("ImGui.BeginDisabled(!this.configuration.RegionEnabled)", source, StringComparison.Ordinal);
        Assert.Contains("RegionActiveTextColor", source, StringComparison.Ordinal);
        Assert.Contains("RegionInactiveTextColor", source, StringComparison.Ordinal);
    }

    private static string ReadMainWindowSource()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "BazookaLens",
            "Windows",
            "BazookaLensWindow.cs"));
        return File.ReadAllText(path);
    }
}
