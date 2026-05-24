using BazookaLens.Capture;

namespace BazookaLens.Commands;

internal sealed record ParsedBlensCommand(
    BlensCommand Command,
    CaptureOptions? CaptureOptions = null,
    ReShadeEventsAction? ReShadeEventsAction = null,
    ResizeProbeOptions? ResizeProbeOptions = null,
    RestoreDisplayOptions? RestoreDisplayOptions = null,
    double? ShootScaleOverride = null);
