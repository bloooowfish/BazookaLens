namespace BazookaLens.Tests;

public sealed class ScaleUiTests
{
    [Fact]
    public void ScaleControlsDoNotRenderApplyButton()
    {
        Assert.DoesNotContain("Apply##Scale", ReadMainWindowSource(), StringComparison.Ordinal);
    }

    [Fact]
    public void ScaleControlsExposeExpandedPresetButtons()
    {
        var source = ReadMainWindowSource();

        foreach (var preset in new[] { "\"1x\"", "\"1.5x\"", "\"2x\"", "\"2.5x\"", "\"3x\"" })
            Assert.Contains(preset, source, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomScaleInputSubmitsOnEnter()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("ImGuiInputTextFlags.EnterReturnsTrue", source, StringComparison.Ordinal);
        Assert.Contains("var commitDraft = submitted ||", source, StringComparison.Ordinal);
        Assert.Contains("this.ApplyScaleDraft();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomScaleInputCommitsWhenFocusLeavesAfterEdit()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("ImGui.IsItemDeactivatedAfterEdit()", source, StringComparison.Ordinal);
        Assert.Contains("var commitDraft = submitted || ImGui.IsItemDeactivatedAfterEdit();", source, StringComparison.Ordinal);
        Assert.Contains("if (commitDraft)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ScaleControlsDisplayPolicyMaxScale()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("Max = ", source, StringComparison.Ordinal);
        Assert.Contains("CaptureScalePolicy.MaxScale", source, StringComparison.Ordinal);
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
