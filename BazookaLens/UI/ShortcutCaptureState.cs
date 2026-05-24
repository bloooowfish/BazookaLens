using Dalamud.Game.ClientState.Keys;

namespace BazookaLens.UI;

internal sealed class ShortcutCaptureState
{
    private static readonly VirtualKey Escape = (VirtualKey)0x1B;
    private static readonly VirtualKey Backspace = (VirtualKey)0x08;
    private static readonly VirtualKey Delete = (VirtualKey)0x2E;

    private readonly List<VirtualKey> pendingKeys = [];

    public IReadOnlyList<VirtualKey> PendingKeys => this.pendingKeys;

    public KeyboardShortcut? Shortcut { get; private set; }

    public string? Error { get; private set; }

    public bool IsComplete => this.Shortcut is not null;

    public bool IsCanceled { get; private set; }

    public static IReadOnlyList<VirtualKey> OrderNewKeysForRecording(
        IReadOnlyCollection<VirtualKey> currentPressedKeys,
        IReadOnlyCollection<VirtualKey> previousPressedKeys)
    {
        return currentPressedKeys
            .Where(key => !previousPressedKeys.Contains(key))
            .Select(KeyboardShortcutValidator.NormalizeKey)
            .Distinct()
            .OrderBy(key => KeyboardShortcutValidator.IsModifier(key) ? 0 : 1)
            .ThenBy(GetModifierSortOrder)
            .ThenBy(key => (int)key)
            .ToArray();
    }

    public ShortcutCaptureResult RecordKey(VirtualKey key)
    {
        this.Error = null;

        if (key == Escape)
        {
            this.Clear();
            this.IsCanceled = true;
            return ShortcutCaptureResult.Canceled();
        }

        if (key == Backspace || key == Delete)
        {
            this.Clear();
            return ShortcutCaptureResult.Cleared();
        }

        this.IsCanceled = false;
        this.Shortcut = null;

        if (!KeyboardShortcutValidator.IsKeyboardKey(key))
            return this.Invalid("Shortcut must use keyboard keys only.");

        var normalized = KeyboardShortcutValidator.NormalizeKey(key);
        if (KeyboardShortcutValidator.IsModifier(normalized))
            return this.RecordModifier(normalized);

        var keys = this.pendingKeys.Append(normalized).ToArray();
        if (!KeyboardShortcutValidator.TryCreate(keys, out var shortcut, out var error))
            return this.Invalid(error ?? "Shortcut is invalid.");

        this.pendingKeys.Clear();
        this.Shortcut = shortcut;
        return ShortcutCaptureResult.Completed(shortcut!);
    }

    private ShortcutCaptureResult RecordModifier(VirtualKey modifier)
    {
        if (this.pendingKeys.Contains(modifier))
            return this.Invalid("Shortcut cannot include duplicate modifiers.");

        if (this.pendingKeys.Count >= 2)
            return this.Invalid("Shortcut can include at most two modifiers.");

        this.pendingKeys.Add(modifier);
        return ShortcutCaptureResult.Pending();
    }

    private ShortcutCaptureResult Invalid(string error)
    {
        this.Error = error;
        return ShortcutCaptureResult.Invalid(error);
    }

    private void Clear()
    {
        this.pendingKeys.Clear();
        this.Shortcut = null;
        this.Error = null;
        this.IsCanceled = false;
    }

    private static int GetModifierSortOrder(VirtualKey key)
    {
        return (int)key switch
        {
            0x11 => 0,
            0x10 => 1,
            0x12 => 2,
            _ => 3,
        };
    }
}

internal sealed record ShortcutCaptureResult(
    bool IsComplete,
    bool IsCanceled,
    bool IsCleared,
    KeyboardShortcut? Shortcut,
    string? Error)
{
    public static ShortcutCaptureResult Pending() => new(false, false, false, null, null);

    public static ShortcutCaptureResult Completed(KeyboardShortcut shortcut) => new(true, false, false, shortcut, null);

    public static ShortcutCaptureResult Canceled() => new(false, true, false, null, null);

    public static ShortcutCaptureResult Cleared() => new(false, false, true, null, null);

    public static ShortcutCaptureResult Invalid(string error) => new(false, false, false, null, error);
}
