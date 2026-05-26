namespace BazookaLens.Capture;

internal sealed class CaptureSettingsProvider
{
    private readonly Configuration configuration;

    public CaptureSettingsProvider(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public string? ConfiguredSaveDirectory => this.configuration.SaveDirectory;

    public CaptureOptions CreateShootOptions(double? scaleOverride)
    {
        var scale = CaptureScalePolicy.NormalizeOrThrow(scaleOverride ?? this.configuration.Scale);
        var region = this.configuration.RegionEnabled ? this.configuration.Region : null;
        return new CaptureOptions(CaptureTiming.AfterImGui, HideGameUi: true, region, scale);
    }

    public CaptureSettings CreateSettings()
    {
        return new CaptureSettings(
            this.configuration.Scale,
            this.configuration.RegionEnabled,
            this.configuration.Region,
            this.configuration.SaveDirectory,
            this.configuration.ImageFormat);
    }
}
