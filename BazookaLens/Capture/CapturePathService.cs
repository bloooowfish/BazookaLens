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

    public string CreateOutputPath(CaptureRegion? region)
    {
        var directory = this.GetScreenshotDirectory();

        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        var id = Interlocked.Increment(ref this.sequence);
        var regionSuffix = region is { } r
            ? $"-x{r.X}-y{r.Y}-w{r.Width}-h{r.Height}"
            : "-full";
        var fileName = $"bazooka-lens-{timestamp}-{id:0000}{regionSuffix}.png";
        var path = Path.Combine(directory, fileName);

        PluginServices.Log.Debug("Capture output path allocated: {Path}", path);
        return path;
    }
}
