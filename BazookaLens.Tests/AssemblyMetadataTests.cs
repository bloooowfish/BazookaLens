using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

using BazookaLens.Capture;

namespace BazookaLens.Tests;

public sealed class AssemblyMetadataTests
{
    [Fact]
    public void ProductNameIsNotBlankForReShadeAddonRegistration()
    {
        var info = FileVersionInfo.GetVersionInfo(typeof(CaptureRegion).Assembly.Location);

        Assert.False(string.IsNullOrWhiteSpace(info.ProductName));
    }

    [Fact]
    public void ManifestUsesSubaccountAuthor()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(ProjectPath("BazookaLens", "BazookaLens.json")));

        Assert.Equal("bloooowfish", json.RootElement.GetProperty("Author").GetString());
    }

    [Fact]
    public void PackageProjectUrlUsesSubaccountRepository()
    {
        var project = XDocument.Load(ProjectPath("BazookaLens", "BazookaLens.csproj"));
        var projectUrl = project.Descendants("PackageProjectUrl").Single().Value;

        Assert.Equal("https://github.com/bloooowfish/BazookaLens", projectUrl);
    }

    private static string ProjectPath(params string[] parts)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", string.Join(Path.DirectorySeparatorChar, parts)));
    }
}
