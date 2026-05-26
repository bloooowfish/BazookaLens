using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace BazookaLens.Capture;

internal sealed class CapturePathService
{
    private readonly Func<string?> configuredDirectoryProvider;
    private long sequence;

    public CapturePathService(Func<string?>? configuredDirectoryProvider = null)
    {
        this.configuredDirectoryProvider = configuredDirectoryProvider ?? (() => null);
    }

    public static string ResolveScreenshotDirectory(string defaultDirectory, string? configuredDirectory)
    {
        return string.IsNullOrWhiteSpace(configuredDirectory)
            ? defaultDirectory
            : configuredDirectory;
    }

    public string GetScreenshotDirectory()
    {
        var defaultDirectory = Path.Combine(PluginServices.PluginInterface.ConfigDirectory.FullName, "Screenshots");
        var directory = ResolveScreenshotDirectory(defaultDirectory, this.configuredDirectoryProvider());
        Directory.CreateDirectory(directory);
        return directory;
    }

    public string CreateOutputPath(CaptureRegion? region, string extension)
    {
        var directory = this.GetScreenshotDirectory();

        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        var id = Interlocked.Increment(ref this.sequence);
        var fileName = CreateFileName(timestamp, id, region, extension);
        var path = Path.Combine(directory, fileName);

        PluginServices.Log.Debug("Capture output path allocated: {Path}", path);
        return path;
    }

    public static string CreateFileName(string timestamp, long id, CaptureRegion? region, string extension)
    {
        var regionSuffix = region is { } r
            ? $"-x{r.X}-y{r.Y}-w{r.Width}-h{r.Height}"
            : "-full";
        return $"bazooka-lens-{timestamp}-{id:0000}{regionSuffix}{NormalizeExtension(extension)}";
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            throw new ArgumentException("File extension must be provided.", nameof(extension));

        return extension.StartsWith(".", StringComparison.Ordinal)
            ? extension
            : $".{extension}";
    }
}
