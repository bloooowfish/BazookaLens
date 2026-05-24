using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using BazookaLens.Capture;
using BazookaLens.UI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace BazookaLens.Windows;

internal sealed class BazookaLensWindow : Window, IDisposable
{
    private const int CustomScaleInputLength = 16;
    private const int SavePathInputLength = 512;
    private const float CustomScaleInputWidth = 110f;
    private const float NumberInputWidth = 80f;

    private static readonly Vector4 SectionTitleColor = new(0.42f, 0.78f, 1f, 1f);
    private static readonly Vector4 ErrorTextColor = new(1f, 0.35f, 0.35f, 1f);
    private static readonly Vector2 InitialWindowSize = new(420, 560);
    private static readonly Vector2 ShootButtonSize = new(120, 32);

    private static readonly string[] GuideModeLabels =
    [
        "None",
        "Rule of Thirds",
        "Center Cross",
        "Grid",
        "Golden",
    ];

    private readonly Configuration configuration;
    private readonly Func<Task> shootAsync;
    private readonly CaptureUiState captureUiState;
    private readonly CapturePathService pathService;
    private readonly RegionOverlayWindow regionOverlayWindow;
    private readonly ShortcutCaptureOverlayWindow shortcutOverlayWindow;
    private string customScaleText;
    private string savePathDraft;
    private string? scaleError;
    private string? savePathError;

    public BazookaLensWindow(
        Configuration configuration,
        Func<Task> shootAsync,
        CaptureUiState captureUiState,
        CapturePathService pathService,
        RegionOverlayWindow regionOverlayWindow,
        ShortcutCaptureOverlayWindow shortcutOverlayWindow)
        : base("Bazooka Lens###BazookaLensMain")
    {
        this.configuration = configuration;
        this.shootAsync = shootAsync;
        this.captureUiState = captureUiState;
        this.pathService = pathService;
        this.regionOverlayWindow = regionOverlayWindow;
        this.shortcutOverlayWindow = shortcutOverlayWindow;
        this.customScaleText = FormatScale(configuration.Scale);
        this.savePathDraft = configuration.SaveDirectory ?? pathService.GetScreenshotDirectory();
        this.Size = InitialWindowSize;
        this.SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose()
    {
    }

    public override bool DrawConditions()
    {
        return !this.regionOverlayWindow.IsSuppressed && !this.shortcutOverlayWindow.IsSuppressed;
    }

    public override void Draw()
    {
        var textInputActive = false;

        this.DrawScaleControls(ref textInputActive);
        ImGui.Separator();
        this.DrawSavePathControls(ref textInputActive);
        ImGui.Separator();
        this.DrawRegionControls(ref textInputActive);
        ImGui.Separator();
        this.DrawGuideControls();
        ImGui.Separator();
        this.DrawShortcutControls();
        ImGui.Separator();
        this.DrawShootControls();

        this.captureUiState.IsTextInputActive = textInputActive;
    }

    private void DrawScaleControls(ref bool textInputActive)
    {
        DrawSectionTitle("Scale");
        this.DrawScalePreset("1x", 1.0);
        ImGui.SameLine();
        this.DrawScalePreset("1.5x", 1.5);
        ImGui.SameLine();
        this.DrawScalePreset("2x", 2.0);

        ImGui.SetNextItemWidth(CustomScaleInputWidth);
        if (ImGui.InputText("##CustomScale", ref this.customScaleText, CustomScaleInputLength))
            this.ValidateScaleDraft();
        textInputActive |= ImGui.IsItemActive();

        ImGui.SameLine();
        if (ImGui.Button("Apply##Scale"))
            this.ApplyScaleDraft();

        if (this.scaleError is not null)
            ImGui.TextColored(ErrorTextColor, this.scaleError);
    }

    private void DrawScalePreset(string label, double scale)
    {
        if (ImGui.Button(label))
        {
            this.configuration.Scale = scale;
            this.configuration.Save();
            this.customScaleText = FormatScale(scale);
            this.scaleError = null;
            this.captureUiState.HasInvalidScaleDraft = false;
        }
    }

    private void DrawSavePathControls(ref bool textInputActive)
    {
        DrawSectionTitle("Save Path");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("##SavePath", ref this.savePathDraft, SavePathInputLength))
            this.savePathError = null;
        textInputActive |= ImGui.IsItemActive();

        if (ImGui.Button("Apply##SavePath"))
            this.ApplySavePathDraft();
        ImGui.SameLine();
        if (ImGui.Button("Use Default##SavePath"))
        {
            this.configuration.SaveDirectory = null;
            this.configuration.Save();
            this.savePathDraft = this.pathService.GetScreenshotDirectory();
            this.savePathError = null;
        }

        ImGui.SameLine();
        if (ImGui.Button("Open Folder##SavePath"))
            this.OpenScreenshotFolder();

        if (this.savePathError is not null)
            ImGui.TextColored(ErrorTextColor, this.savePathError);
    }

    private void DrawRegionControls(ref bool textInputActive)
    {
        DrawSectionTitle("Region");
        var (viewportWidth, viewportHeight) = GetViewportSize();

        if (ImGui.Button("Full Frame"))
        {
            this.configuration.Region = CaptureRegion.Full(viewportWidth, viewportHeight);
            this.configuration.RegionEnabled = false;
            this.configuration.Save();
        }

        ImGui.SameLine();
        if (ImGui.Button("Use Region"))
            this.ResetRegionFromViewport();

        ImGui.SameLine();
        if (ImGui.Button(this.regionOverlayWindow.IsOpen ? "Close Overlay" : "Edit Overlay"))
        {
            if (this.regionOverlayWindow.IsOpen)
            {
                this.regionOverlayWindow.CloseEditor();
            }
            else
            {
                this.EnableRegionForEditingFromViewport();
                this.regionOverlayWindow.OpenEditor();
            }
        }

        ImGui.TextUnformatted(this.configuration.RegionEnabled ? "Mode: Region" : "Mode: Full frame");

        var region = this.configuration.Region ?? CaptureRegion.Full(viewportWidth, viewportHeight);
        var x = region.X;
        var y = region.Y;
        var width = region.Width;
        var height = region.Height;

        ImGui.SetNextItemWidth(NumberInputWidth);
        var changed = ImGui.InputInt("X", ref x);
        textInputActive |= ImGui.IsItemActive();
        ImGui.SameLine();
        ImGui.SetNextItemWidth(NumberInputWidth);
        changed |= ImGui.InputInt("Y", ref y);
        textInputActive |= ImGui.IsItemActive();
        ImGui.SetNextItemWidth(NumberInputWidth);
        changed |= ImGui.InputInt("W", ref width);
        textInputActive |= ImGui.IsItemActive();
        ImGui.SameLine();
        ImGui.SetNextItemWidth(NumberInputWidth);
        changed |= ImGui.InputInt("H", ref height);
        textInputActive |= ImGui.IsItemActive();

        if (changed)
        {
            var viewport = ImGui.GetMainViewport();
            this.configuration.Region = ClampRegion(
                new CaptureRegion(x, y, width, height),
                Math.Max(1, (int)viewport.Size.X),
                Math.Max(1, (int)viewport.Size.Y));
            this.configuration.RegionEnabled = true;
            this.configuration.Save();
        }
    }

    private void DrawGuideControls()
    {
        DrawSectionTitle("Guides");
        var guideIndex = Math.Clamp((int)this.configuration.GuideMode, 0, GuideModeLabels.Length - 1);
        if (ImGui.Combo("Guide Mode", ref guideIndex, GuideModeLabels, GuideModeLabels.Length))
        {
            this.configuration.GuideMode = (GuideMode)guideIndex;
            this.configuration.Save();
        }

        if (this.configuration.GuideMode != GuideMode.Grid)
            return;

        var rows = this.configuration.GridRows;
        var columns = this.configuration.GridColumns;
        ImGui.SetNextItemWidth(NumberInputWidth);
        var changed = ImGui.InputInt("Rows", ref rows);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(NumberInputWidth);
        changed |= ImGui.InputInt("Columns", ref columns);
        if (changed)
        {
            this.configuration.GridRows = GuideLayout.ClampGridDivision(rows);
            this.configuration.GridColumns = GuideLayout.ClampGridDivision(columns);
            this.configuration.Save();
        }
    }

    private void DrawShortcutControls()
    {
        DrawSectionTitle("Shortcut");
        ImGui.TextUnformatted($"Current: {KeyboardShortcut.Format(this.configuration.Shortcut)}");
        if (ImGui.Button("Set Shortcut"))
            this.shortcutOverlayWindow.OpenRecording();
        ImGui.SameLine();
        if (ImGui.Button("Clear Shortcut"))
        {
            this.configuration.Shortcut = null;
            this.configuration.Save();
        }
    }

    private void DrawShootControls()
    {
        DrawSectionTitle("Shoot");
        var canShoot = this.captureUiState.CanTriggerInteractiveCapture;
        if (!canShoot)
            ImGui.BeginDisabled();

        if (ImGui.Button("Shoot", ShootButtonSize))
            _ = this.shootAsync();

        if (!canShoot)
            ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.TextUnformatted(this.GetAvailabilityText());
    }

    private string GetAvailabilityText()
    {
        if (this.captureUiState.IsCapturing)
            return "Capturing...";
        if (this.captureUiState.IsShortcutRecording)
            return "Shortcut recording...";
        if (this.captureUiState.HasInvalidScaleDraft)
            return "Finish editing scale";
        if (this.captureUiState.IsTextInputActive)
            return "Editing text";
        return "Ready";
    }

    private void ValidateScaleDraft()
    {
        if (TryParseScaleDraft(this.customScaleText, out _, out var error))
        {
            this.scaleError = null;
            this.captureUiState.HasInvalidScaleDraft = false;
            return;
        }

        this.scaleError = error;
        this.captureUiState.HasInvalidScaleDraft = true;
    }

    private void ApplyScaleDraft()
    {
        if (!TryParseScaleDraft(this.customScaleText, out var scale, out var error))
        {
            this.scaleError = error;
            this.captureUiState.HasInvalidScaleDraft = true;
            return;
        }

        this.configuration.Scale = scale;
        this.configuration.Save();
        this.customScaleText = FormatScale(this.configuration.Scale);
        this.scaleError = null;
        this.captureUiState.HasInvalidScaleDraft = false;
    }

    private void ApplySavePathDraft()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(this.savePathDraft))
            {
                this.configuration.SaveDirectory = null;
                this.savePathDraft = this.pathService.GetScreenshotDirectory();
            }
            else
            {
                Directory.CreateDirectory(this.savePathDraft);
                this.configuration.SaveDirectory = this.savePathDraft;
            }

            this.configuration.Save();
            this.savePathError = null;
        }
        catch (Exception ex)
        {
            this.savePathError = ex.Message;
            PluginServices.Log.Warning(ex, "Failed to apply configured screenshot directory.");
        }
    }

    private void OpenScreenshotFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = this.pathService.GetScreenshotDirectory(),
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            this.savePathError = ex.Message;
            PluginServices.Log.Warning(ex, "Failed to open screenshot directory.");
        }
    }

    private void ResetRegionFromViewport()
    {
        var (viewportWidth, viewportHeight) = GetViewportSize();

        this.configuration.Region = RegionSelectionDefaults.CreateCenteredRegion(viewportWidth, viewportHeight);
        this.configuration.RegionEnabled = true;
        this.configuration.Save();
    }

    private void EnableRegionForEditingFromViewport()
    {
        var (viewportWidth, viewportHeight) = GetViewportSize();

        this.configuration.Region = this.configuration.RegionEnabled
            ? RegionSelectionDefaults.CreateCenteredOrClampExisting(this.configuration.Region, viewportWidth, viewportHeight)
            : RegionSelectionDefaults.CreateCenteredRegion(viewportWidth, viewportHeight);
        this.configuration.RegionEnabled = true;
        this.configuration.Save();
    }

    private static void DrawSectionTitle(string title)
    {
        ImGui.Spacing();
        ImGui.TextColored(SectionTitleColor, $"[ {title} ]");
    }

    private static (int Width, int Height) GetViewportSize()
    {
        var viewport = ImGui.GetMainViewport();
        return (
            Math.Max(1, (int)viewport.Size.X),
            Math.Max(1, (int)viewport.Size.Y));
    }

    private static CaptureRegion ClampRegion(CaptureRegion region, int viewportWidth, int viewportHeight)
    {
        return RegionSelectionDefaults.ClampRegion(region, viewportWidth, viewportHeight);
    }

    private static bool TryParseScaleDraft(string text, out double scale, out string? error)
    {
        scale = 0;
        error = null;
        var trimmed = text.Trim();
        var decimalIndex = trimmed.IndexOf('.', StringComparison.Ordinal);
        if (decimalIndex >= 0 && trimmed.Length - decimalIndex - 1 > 2)
        {
            error = "Scale accepts up to two decimal places.";
            return false;
        }

        if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out scale))
        {
            error = "Scale must be a number.";
            return false;
        }

        scale = CaptureScalePolicy.Normalize(scale);
        if (!CaptureScalePolicy.IsValid(scale, out error))
            return false;

        return true;
    }

    private static string FormatScale(double scale)
    {
        return scale.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
