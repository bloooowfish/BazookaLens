using System.Numerics;
using BazookaLens.UI;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Windowing;

namespace BazookaLens.Windows;

internal sealed class ShortcutCaptureOverlayWindow : Window, IDisposable
{
    private static readonly Vector2 PanelSize = new(360, 160);
    private static readonly Vector2 PanelPadding = new(18, 16);
    private static readonly Vector4 BackdropColor = new(0f, 0f, 0f, 0.62f);
    private static readonly Vector4 PanelFillColor = new(0.08f, 0.09f, 0.11f, 0.96f);
    private static readonly Vector4 PanelBorderColor = new(1f, 1f, 1f, 0.18f);
    private static readonly Vector4 ErrorTextColor = new(1f, 0.35f, 0.35f, 1f);

    private readonly Configuration configuration;
    private readonly OwnUiSuppressionController ownUiSuppressionController;
    private readonly CaptureUiState captureUiState;
    private readonly HashSet<VirtualKey> previousPressedKeys = [];
    private ShortcutCaptureState captureState = new();
    private string? error;

    public ShortcutCaptureOverlayWindow(
        Configuration configuration,
        OwnUiSuppressionController ownUiSuppressionController,
        CaptureUiState captureUiState)
        : base(
            "Bazooka Lens Shortcut Overlay###BazookaLensShortcutOverlay",
            ImGuiWindowFlags.NoDecoration |
            ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoScrollWithMouse |
            ImGuiWindowFlags.NoBackground,
            forceMainWindow: true)
    {
        this.configuration = configuration;
        this.ownUiSuppressionController = ownUiSuppressionController;
        this.captureUiState = captureUiState;
        this.ShowCloseButton = false;
        this.RespectCloseHotkey = false;
        this.AllowClickthrough = false;
    }

    public bool IsSuppressed => this.ownUiSuppressionController.IsSuppressed;

    public void Dispose()
    {
        this.captureUiState.IsShortcutRecording = false;
    }

    public void OpenRecording()
    {
        this.captureState = new ShortcutCaptureState();
        this.previousPressedKeys.Clear();
        this.error = null;
        this.captureUiState.IsShortcutRecording = true;
        this.IsOpen = true;
    }

    public override bool DrawConditions()
    {
        return !this.ownUiSuppressionController.IsSuppressed;
    }

    public override void PreDraw()
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.Pos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(viewport.Size, ImGuiCond.Always);
    }

    public override void Draw()
    {
        this.captureUiState.IsShortcutRecording = true;
        var viewport = ImGui.GetMainViewport();
        var drawList = ImGui.GetWindowDrawList();
        var viewportPos = viewport.Pos;
        var viewportSize = viewport.Size;
        var viewportMax = viewportPos + viewportSize;
        drawList.AddRectFilled(viewportPos, viewportMax, ImGui.GetColorU32(BackdropColor));

        var panelPos = viewportPos + (viewportSize - PanelSize) / 2f;
        drawList.AddRectFilled(panelPos, panelPos + PanelSize, ImGui.GetColorU32(PanelFillColor), 6f);
        drawList.AddRect(panelPos, panelPos + PanelSize, ImGui.GetColorU32(PanelBorderColor), 6f);

        ImGui.SetCursorScreenPos(panelPos + PanelPadding);
        ImGui.BeginGroup();
        ImGui.TextUnformatted("Press shortcut");
        ImGui.TextUnformatted("Esc cancels. Backspace/Delete clears.");
        ImGui.Spacing();
        ImGui.TextUnformatted($"Current: {this.FormatPendingKeys()}");
        if (this.error is not null)
            ImGui.TextColored(ErrorTextColor, this.error);

        if (ImGui.Button("Cancel##ShortcutCapture"))
            this.CloseRecording(clearShortcut: false);
        ImGui.EndGroup();

        this.ScanPressedKeys();
    }

    private void ScanPressedKeys()
    {
        var currentPressedKeys = PluginServices.KeyState.GetValidVirtualKeys()
            .Where(key => PluginServices.KeyState[key])
            .ToHashSet();

        var newlyPressedKeys = ShortcutCaptureState.OrderNewKeysForRecording(currentPressedKeys, this.previousPressedKeys);

        foreach (var key in newlyPressedKeys)
        {
            var result = this.captureState.RecordKey(key);
            if (result.IsComplete)
            {
                this.configuration.Shortcut = result.Shortcut;
                this.configuration.Save();
                this.CloseRecording(clearShortcut: false);
                break;
            }

            if (result.IsCleared)
            {
                this.configuration.Shortcut = null;
                this.configuration.Save();
                this.CloseRecording(clearShortcut: true);
                break;
            }

            if (result.IsCanceled)
            {
                this.CloseRecording(clearShortcut: false);
                break;
            }

            this.error = result.Error;
        }

        this.previousPressedKeys.Clear();
        foreach (var key in currentPressedKeys)
            this.previousPressedKeys.Add(key);
    }

    private void CloseRecording(bool clearShortcut)
    {
        if (clearShortcut)
            this.configuration.Shortcut = null;

        this.captureUiState.IsShortcutRecording = false;
        this.IsOpen = false;
    }

    private string FormatPendingKeys()
    {
        if (this.captureState.PendingKeys.Count == 0)
            return KeyboardShortcut.Format(this.configuration.Shortcut);

        return string.Join("+", this.captureState.PendingKeys.Select(key => new KeyboardShortcut([key]).ToString()));
    }
}
