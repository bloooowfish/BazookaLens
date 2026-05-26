namespace BazookaLens.Tests;

public sealed class SavePathUiTests
{
    [Fact]
    public void SavePathControlsDoNotRenderSeparateApplyButton()
    {
        Assert.DoesNotContain("Apply##SavePath", ReadMainWindowSource(), StringComparison.Ordinal);
    }

    [Fact]
    public void SavePathControlsUseFolderIconDialog()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("FontAwesomeIcon.FolderOpen", source, StringComparison.Ordinal);
        Assert.Contains("OpenFolderDialog", source, StringComparison.Ordinal);
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
