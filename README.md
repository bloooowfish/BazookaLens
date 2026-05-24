# Bazooka Lens

Bazooka Lens is a Dalamud plugin for native high-resolution FFXIV screenshots.

It can temporarily resize the game render target, hide the game UI, capture the full frame or a configured region, and restore the original resolution afterward. When ReShade addon events are available, scaled captures use the post-effects texture so the saved image includes the active preset.

## Custom repository URL

```text
https://raw.githubusercontent.com/bloooowfish/BazookaLens/refs/heads/main/repo.json
```

## Quick Use

Open the plugin window from Dalamud, then use **Shoot**.

Default settings:

- Scale: `2.00`
- Capture mode: full frame
- Guide overlay: rule of thirds
- Grid defaults: `3 x 3`
- Save path: Bazooka Lens plugin screenshot folder

Scale presets are `1x`, `1.5x`, and `2x`. Custom scale accepts values greater than `0` and up to `4.00`; the UI accepts at most two decimal places.

Plugin window sections:

- **Scale**: choose `1x`, `1.5x`, `2x`, or type a custom scale and press **Apply**.
- **Save Path**: press **Apply**, **Use Default**, or **Open Folder**.
- **Region**: switch between **Full Frame**, **Use Region**, and **Edit Overlay**.
- **Guides**: choose `None`, `Rule of Thirds`, `Center Cross`, `Grid`, or `Golden`. Grid mode also exposes **Rows** and **Columns**.
- **Shortcut**: shows `Current: (not set)` until a shortcut is configured. Use **Set Shortcut** or **Clear Shortcut**.
- **Shoot**: starts a settings-backed capture when the status text is `Ready`.

Status text can show `Capturing...`, `Shortcut recording...`, `Finish editing scale`, `Editing text`, or `Ready`.

## Commands
Everything you can do in the GUI can also be done with commands.
But I doubt anyone would go out of their way to use commands, right?

All commands use `/blens`.

```text
/blens help
```

Prints the command summary.

```text
/blens shoot [scale]
```

Captures using the plugin window settings. If `scale` is provided, it overrides the configured scale for that one shot.

```text
/blens open-folder
```

Opens the screenshot folder.

```text
/blens status
/blens reshade-status
/blens reshade-events start|stop|status
```

Writes runtime and ReShade diagnostic status to `/xllog`.

Manual capture/debug commands are also available:

```text
/blens capture [before|after] [hide-ui]
/blens capture-scale scale [before|after] [hide-ui]
/blens capture-region x y w h [before|after] [hide-ui]
/blens capture-region-scale x y w h scale [before|after] [hide-ui]
/blens resize-probe scale [dry-run|device]
/blens restore-ui
/blens restore-display [force]
```

For normal use, prefer `/blens shoot [scale]` or the GUI **Shoot** button.

## Region Capture

The region coordinate origin is the top-left corner of the current game viewport.

In the plugin window:

- **Full Frame** switches back to full-frame capture and updates the numeric fields to the full viewport.
- **Use Region** initializes a centered region at 75% of the current viewport.
- **Edit Overlay** opens the region overlay without resetting an existing enabled region.

In the overlay:

- Drag inside the rectangle to move the region.
- Drag the inside corner brackets to resize.
- Right-click to close the overlay.
- Guide modes include none, rule of thirds, center cross, grid, and golden ratio.

## Shortcut Capture

Use **Set Shortcut** in the plugin window to record a keyboard shortcut.

Shortcut rules:

- Keyboard only for now.
- At most three keys.
- The first two keys, if present, must be modifiers: `Ctrl`, `Shift`, or `Alt`.
- Normal keys require at least one modifier.
- Function keys can be used without a modifier.

## ReShade Behavior

When ReShade addon exports are detected, Bazooka Lens can auto-start its event bridge and capture from `ReShadeFinishEffects` after the scaled render target has settled.

The capture request ignores pre-resize textures whose dimensions do not match the expected scaled target. This avoids saving stale 2560x1440 frames during a 3840x2160 or 5120x2880 capture.

If ReShade post-effects capture is unavailable, Bazooka Lens falls back to the viewport capture path.

## Restore Behavior

Scaled capture requests restore the original device resolution after saving.

During restore, Bazooka Lens currently prioritizes returning the game resolution to the previous target. That means plugin unload or cancellation can wait for bounded restore/settle work instead of stopping immediately. This policy is intentional for the first baseline and should be revisited after unload/reload QA.
