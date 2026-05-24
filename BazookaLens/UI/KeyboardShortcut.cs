using System;
using Dalamud.Game.ClientState.Keys;

namespace BazookaLens.UI;

[Serializable]
internal sealed record KeyboardShortcut(IReadOnlyList<VirtualKey> Keys)
{
    public VirtualKey MainKey => this.Keys.Count == 0 ? VirtualKey.NO_KEY : this.Keys[^1];

    public IReadOnlyList<VirtualKey> Modifiers => this.Keys.Count <= 1
        ? []
        : this.Keys.Take(this.Keys.Count - 1).ToArray();

    public static string Format(KeyboardShortcut? shortcut)
    {
        return shortcut?.ToString() ?? "(not set)";
    }

    public override string ToString()
    {
        return string.Join("+", this.Keys.Select(FormatKey));
    }

    private static string FormatKey(VirtualKey key)
    {
        return (int)key switch
        {
            0x11 => "Ctrl",
            0x10 => "Shift",
            0x12 => "Alt",
            >= 0x70 and <= 0x87 => $"F{(int)key - 0x6F}",
            >= 0x30 and <= 0x39 => ((char)(int)key).ToString(),
            >= 0x41 and <= 0x5A => ((char)(int)key).ToString(),
            _ => key.ToString(),
        };
    }
}
