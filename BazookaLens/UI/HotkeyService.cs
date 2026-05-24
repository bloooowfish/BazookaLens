using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;

namespace BazookaLens.UI;

internal sealed class HotkeyService
{
    private readonly IKeyState keyState;
    private readonly Func<KeyboardShortcut?> shortcutProvider;
    private readonly Func<bool> canTrigger;
    private readonly Func<Task> triggerAsync;
    private KeyboardShortcut? previousShortcut;
    private bool wasPressed;

    public HotkeyService(
        IKeyState keyState,
        Func<KeyboardShortcut?> shortcutProvider,
        Func<bool> canTrigger,
        Func<Task> triggerAsync)
    {
        this.keyState = keyState;
        this.shortcutProvider = shortcutProvider;
        this.canTrigger = canTrigger;
        this.triggerAsync = triggerAsync;
    }

    public static bool IsPressed(KeyboardShortcut? shortcut, IReadOnlyCollection<VirtualKey> pressedKeys)
    {
        if (shortcut is null)
            return false;

        var normalizedPressedKeys = pressedKeys
            .Where(KeyboardShortcutValidator.IsKeyboardKey)
            .Select(KeyboardShortcutValidator.NormalizeKey)
            .Distinct()
            .ToArray();

        return normalizedPressedKeys.Length == shortcut.Keys.Count
            && shortcut.Keys.All(normalizedPressedKeys.Contains);
    }

    public static bool ShouldTrigger(
        KeyboardShortcut? currentShortcut,
        KeyboardShortcut? previousShortcut,
        bool pressed,
        bool wasPressed,
        bool canTrigger)
    {
        return currentShortcut is not null
            && SameShortcut(currentShortcut, previousShortcut)
            && pressed
            && !wasPressed
            && canTrigger;
    }

    public void OnFrameworkUpdate(IFramework framework)
    {
        var shortcut = this.shortcutProvider();
        var pressed = shortcut is not null && this.IsPressed(shortcut);
        var canTriggerNow = pressed
            && !this.wasPressed
            && SameShortcut(shortcut, this.previousShortcut)
            && this.canTrigger();

        if (ShouldTrigger(shortcut, this.previousShortcut, pressed, this.wasPressed, canTriggerNow))
            _ = this.triggerAsync();

        this.wasPressed = pressed;
        this.previousShortcut = shortcut;
    }

    private bool IsPressed(KeyboardShortcut shortcut)
    {
        if (!KeyboardShortcutValidator.TryCreate(shortcut.Keys, out _, out _))
            return false;

        var pressedKeys = this.keyState.GetValidVirtualKeys()
            .Where(key => this.keyState[key])
            .ToArray();
        return IsPressed(shortcut, pressedKeys);
    }

    private static bool SameShortcut(KeyboardShortcut? left, KeyboardShortcut? right)
    {
        if (left is null || right is null)
            return left is null && right is null;

        return left.Keys.SequenceEqual(right.Keys);
    }
}
