using System.Numerics;
using BazookaLens.Capture;
using BazookaLens.UI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace BazookaLens.Windows;

internal sealed class RegionOverlayWindow : Window, IDisposable
{
    private const float AnchorSize = RegionOverlayInput.DefaultAnchorSize;
    private const float AnchorInset = RegionOverlayInput.DefaultAnchorInset;
    private const float SectionInstructionAlpha = 0.78f;
    private const float AnchorBracketThickness = 3f;
    private const float AnchorCueBorderThickness = 1f;

    private readonly Configuration configuration;
    private readonly OwnUiSuppressionController ownUiSuppressionController;
    private RegionDragHandle? activeHandle;
    private Vector2 lastMousePos;
    private bool wasRightMousePressed;

    public RegionOverlayWindow(Configuration configuration, OwnUiSuppressionController ownUiSuppressionController)
        : base(
            "Bazooka Lens Region Overlay###BazookaLensRegionOverlay",
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
        this.ShowCloseButton = false;
        this.RespectCloseHotkey = false;
        this.AllowClickthrough = false;
        this.IsClickthrough = false;
    }

    public bool IsSuppressed => this.ownUiSuppressionController.IsSuppressed;

    public void Dispose()
    {
    }

    public void OpenEditor()
    {
        this.wasRightMousePressed = false;
        this.IsOpen = true;
        PluginServices.Log.Debug(
            "Region overlay opened: Region={Region}",
            this.configuration.Region?.ToString() ?? "<none>");
    }

    public void CloseEditor()
    {
        this.CloseEditor("Requested");
    }

    public void CloseEditorForCapture()
    {
        if (!this.IsOpen)
            return;

        this.CloseEditor("CaptureStarted");
    }

    private void CloseEditor(string reason)
    {
        this.configuration.Save();
        var wasOpen = this.IsOpen;
        this.IsOpen = false;
        this.activeHandle = null;
        this.wasRightMousePressed = false;
        PluginServices.Log.Debug(
            "Region overlay closed: Reason={Reason}, WasOpen={WasOpen}, Region={Region}",
            reason,
            wasOpen,
            this.configuration.Region?.ToString() ?? "<none>");
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
        if (this.TryCloseBeforeDrawing())
            return;

        var viewport = ImGui.GetMainViewport();
        var viewportPos = viewport.Pos;
        var viewportSize = viewport.Size;
        var viewportWidth = Math.Max(RegionSelectionState.MinRegionSizePixels, (int)viewportSize.X);
        var viewportHeight = Math.Max(RegionSelectionState.MinRegionSizePixels, (int)viewportSize.Y);

        if (this.configuration.Region is not { } region)
            return;

        var rectMin = viewportPos + new Vector2(region.X, region.Y);
        var rectMax = rectMin + new Vector2(region.Width, region.Height);
        var regionRect = UiScreenRect.FromMinMax(rectMin, rectMax);
        var hoveredHandle = RegionOverlayInput.ResolveHandle(
            ImGui.GetMousePos(),
            regionRect,
            AnchorSize,
            AnchorInset);
        var drawList = ImGui.GetWindowDrawList();

        ImGui.SetCursorScreenPos(viewportPos);
        ImGui.InvisibleButton("##BazookaLensRegionOverlayInputSurface", viewportSize);

        this.DrawOverlay(drawList, viewportPos, viewportSize, rectMin, rectMax, region, hoveredHandle);
        this.UpdateInput(viewportWidth, viewportHeight, hoveredHandle);

        drawList.AddText(
            viewportPos + new Vector2(18, 18),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, SectionInstructionAlpha)),
            "Bazooka Lens region overlay - Right-click or Close Overlay to finish");
    }

    private void DrawOverlay(
        ImDrawListPtr drawList,
        Vector2 viewportPos,
        Vector2 viewportSize,
        Vector2 rectMin,
        Vector2 rectMax,
        CaptureRegion region,
        RegionDragHandle? hoveredHandle)
    {
        var viewportMax = viewportPos + viewportSize;
        var dim = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.58f));
        var border = ImGui.GetColorU32(new Vector4(0.1f, 0.8f, 1f, 1f));
        var anchor = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.95f));
        var anchorCue = ImGui.GetColorU32(new Vector4(0.1f, 0.8f, 1f, 0.12f));
        var anchorCueHovered = ImGui.GetColorU32(new Vector4(0.1f, 0.8f, 1f, 0.26f));
        var anchorCueBorder = ImGui.GetColorU32(new Vector4(0.1f, 0.8f, 1f, 0.48f));
        var guide = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.42f));

        drawList.AddRectFilled(viewportPos, new Vector2(viewportMax.X, rectMin.Y), dim);
        drawList.AddRectFilled(new Vector2(viewportPos.X, rectMax.Y), viewportMax, dim);
        drawList.AddRectFilled(new Vector2(viewportPos.X, rectMin.Y), new Vector2(rectMin.X, rectMax.Y), dim);
        drawList.AddRectFilled(new Vector2(rectMax.X, rectMin.Y), new Vector2(viewportMax.X, rectMax.Y), dim);
        drawList.AddRect(rectMin, rectMax, border, 0f, ImDrawFlags.None, 2f);

        this.DrawGuides(drawList, rectMin, rectMax, guide);
        foreach (var (zone, handle) in CornerHitZones(rectMin, rectMax))
        {
            DrawAnchorCue(drawList, zone, hoveredHandle == handle ? anchorCueHovered : anchorCue, anchorCueBorder);
            DrawAnchorBracket(drawList, zone, handle, border, AnchorBracketThickness);
        }

        var labelPos = rectMin + new Vector2(8, AnchorInset + AnchorSize + 8);
        drawList.AddText(labelPos, anchor, $"X {region.X}  Y {region.Y}  W {region.Width}  H {region.Height}");
    }

    private void DrawGuides(ImDrawListPtr drawList, Vector2 rectMin, Vector2 rectMax, uint color)
    {
        drawList.PushClipRect(rectMin, rectMax, true);
        foreach (var line in GuideLayout.Create(this.configuration.GuideMode, this.configuration.GridColumns, this.configuration.GridRows).Lines)
        {
            if (line.Orientation == GuideLineOrientation.Vertical)
            {
                var x = rectMin.X + (rectMax.X - rectMin.X) * line.NormalizedPosition;
                drawList.AddLine(new Vector2(x, rectMin.Y), new Vector2(x, rectMax.Y), color);
            }
            else
            {
                var y = rectMin.Y + (rectMax.Y - rectMin.Y) * line.NormalizedPosition;
                drawList.AddLine(new Vector2(rectMin.X, y), new Vector2(rectMax.X, y), color);
            }
        }

        drawList.PopClipRect();
    }

    private void UpdateInput(int viewportWidth, int viewportHeight, RegionDragHandle? hoveredHandle)
    {
        if (this.activeHandle is null && hoveredHandle is not null && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            this.activeHandle = hoveredHandle.Value;
            this.lastMousePos = ImGui.GetMousePos();
        }

        if (this.activeHandle is not null && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var mouse = ImGui.GetMousePos();
            var delta = mouse - this.lastMousePos;
            var dx = (int)MathF.Round(delta.X);
            var dy = (int)MathF.Round(delta.Y);
            if (dx != 0 || dy != 0)
            {
                var current = this.configuration.Region!.Value;
                var next = new RegionSelectionState(current.X, current.Y, current.Width, current.Height)
                    .ApplyDrag(this.activeHandle.Value, dx, dy, viewportWidth, viewportHeight);
                this.configuration.Region = new CaptureRegion(next.X, next.Y, next.Width, next.Height);
                this.configuration.RegionEnabled = true;
                this.lastMousePos = mouse;
            }
        }

        if (this.activeHandle is not null && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            this.configuration.Save();
            this.activeHandle = null;
        }
    }

    private bool TryCloseBeforeDrawing()
    {
        var decision = RegionOverlayInput.DecideCloseBeforeDraw(
            ImGui.IsMouseDown(ImGuiMouseButton.Right),
            this.wasRightMousePressed);
        this.wasRightMousePressed = decision.NextWasRightPressed;
        if (!decision.ShouldCloseBeforeDraw)
            return false;

        this.CloseEditor("RightClick");
        return !decision.ShouldDrawOverlay;
    }

    private static (UiScreenRect Zone, RegionDragHandle Handle)[] CornerHitZones(Vector2 rectMin, Vector2 rectMax)
    {
        return RegionOverlayInput.CreateAnchorZones(UiScreenRect.FromMinMax(rectMin, rectMax), AnchorSize, AnchorInset);
    }

    private static void DrawAnchorBracket(
        ImDrawListPtr drawList,
        UiScreenRect zone,
        RegionDragHandle handle,
        uint color,
        float thickness)
    {
        foreach (var segment in RegionOverlayInput.CreateAnchorBracketSegments(zone, handle, thickness))
            DrawFilledRect(drawList, segment, color);
    }

    private static void DrawAnchorCue(ImDrawListPtr drawList, UiScreenRect zone, uint fill, uint border)
    {
        var min = new Vector2(zone.X, zone.Y);
        var max = new Vector2(zone.Right, zone.Bottom);
        drawList.AddRectFilled(min, max, fill, 1f);

        foreach (var segment in CreateInsideRectSegments(zone, AnchorCueBorderThickness))
            DrawFilledRect(drawList, segment, border);
    }

    private static UiScreenRect[] CreateInsideRectSegments(UiScreenRect zone, float thickness)
    {
        var line = MathF.Min(MathF.Max(1f, thickness), MathF.Min(zone.Width, zone.Height));

        return
        [
            new UiScreenRect(zone.X, zone.Y, zone.Width, line),
            new UiScreenRect(zone.X, zone.Bottom - line, zone.Width, line),
            new UiScreenRect(zone.X, zone.Y, line, zone.Height),
            new UiScreenRect(zone.Right - line, zone.Y, line, zone.Height),
        ];
    }

    private static void DrawFilledRect(ImDrawListPtr drawList, UiScreenRect rect, uint color)
    {
        drawList.AddRectFilled(
            new Vector2(rect.X, rect.Y),
            new Vector2(rect.Right, rect.Bottom),
            color);
    }
}
