using Dalamud.Game.ClientState.Keys;

namespace BazookaLens.UI;

internal static class KeyboardShortcutValidator
{
    private const int Control = 0x11;
    private const int Shift = 0x10;
    private const int Alt = 0x12;

    public static bool TryCreate(
        IReadOnlyList<VirtualKey> keys,
        out KeyboardShortcut? shortcut,
        out string? error)
    {
        shortcut = null;
        error = null;

        if (keys.Count == 0)
        {
            error = "Shortcut must include a main key.";
            return false;
        }

        if (keys.Count > 3)
        {
            error = "Shortcut can include at most two modifiers and one main key.";
            return false;
        }

        var normalized = new List<VirtualKey>(keys.Count);
        var modifiers = new HashSet<int>();
        VirtualKey? mainKey = null;

        foreach (var key in keys)
        {
            if (!IsKeyboardKey(key))
            {
                error = "Shortcut must use keyboard keys only.";
                return false;
            }

            var normalizedKey = NormalizeKey(key);
            if (IsModifier(normalizedKey))
            {
                var modifierValue = (int)normalizedKey;
                if (!modifiers.Add(modifierValue))
                {
                    error = "Shortcut cannot include duplicate modifiers.";
                    return false;
                }

                if (mainKey.HasValue)
                {
                    error = "Shortcut modifiers must come before the main key.";
                    return false;
                }

                normalized.Add(normalizedKey);
                continue;
            }

            if (mainKey.HasValue)
            {
                error = "Shortcut can include only one main key.";
                return false;
            }

            mainKey = normalizedKey;
            normalized.Add(normalizedKey);
        }

        if (modifiers.Count > 2)
        {
            error = "Shortcut can include at most two modifiers.";
            return false;
        }

        if (!mainKey.HasValue)
        {
            error = "Shortcut must include a main key.";
            return false;
        }

        if (!IsFunctionKey(mainKey.Value) && modifiers.Count == 0)
        {
            error = "Normal shortcuts require at least one modifier.";
            return false;
        }

        shortcut = new KeyboardShortcut(normalized);
        return true;
    }

    internal static bool IsModifier(VirtualKey key)
    {
        var value = (int)NormalizeKey(key);
        return value is Control or Shift or Alt;
    }

    internal static bool IsFunctionKey(VirtualKey key)
    {
        var value = (int)key;
        return value is >= 0x70 and <= 0x87;
    }

    internal static bool IsKeyboardKey(VirtualKey key)
    {
        var value = (int)key;
        if (value is >= 0x01 and <= 0x06)
            return false;

        return value is >= 0x08 and <= 0xFE;
    }

    internal static VirtualKey NormalizeKey(VirtualKey key)
    {
        var value = (int)key;
        return (VirtualKey)(value switch
        {
            0xA2 or 0xA3 => Control,
            0xA0 or 0xA1 => Shift,
            0xA4 or 0xA5 => Alt,
            _ => value,
        });
    }
}
