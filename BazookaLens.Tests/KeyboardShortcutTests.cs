using BazookaLens;

namespace BazookaLens.Tests;

public sealed class KeyboardShortcutTests
{
    private const int Control = 0x11;
    private const int Shift = 0x10;
    private const int LeftControl = 0xA2;
    private const int RightShift = 0xA1;
    private const int Alt = 0x12;
    private const int P = 0x50;
    private const int F12 = 0x7B;
    private const int Escape = 0x1B;
    private const int Backspace = 0x08;
    private const int Delete = 0x2E;
    private const int LeftMouseButton = 0x01;

    [Fact]
    public void FormatNullShortcutAsNotSet()
    {
        Assert.Equal("(not set)", ShortcutApi.Format(null));
    }

    [Fact]
    public void NormalizeLeftAndRightModifierVariantsForFormatting()
    {
        var shortcut = ShortcutApi.Create(LeftControl, RightShift, P);

        Assert.Equal("Ctrl+Shift+P", shortcut.ToString());
        Assert.Equal([Control, Shift, P], ShortcutApi.Keys(shortcut));
    }

    [Fact]
    public void AllowFunctionKeyWithoutModifier()
    {
        var shortcut = ShortcutApi.Create(F12);

        Assert.Equal("F12", shortcut.ToString());
    }

    [Fact]
    public void RejectNormalMainKeyWithoutModifier()
    {
        var valid = ShortcutApi.TryCreate([P], out var shortcut, out var error);

        Assert.False(valid);
        Assert.Null(shortcut);
        Assert.Contains("modifier", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectModifierOnlyShortcut()
    {
        var valid = ShortcutApi.TryCreate([Control, Shift], out var shortcut, out var error);

        Assert.False(valid);
        Assert.Null(shortcut);
        Assert.Contains("main key", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectMoreThanTwoModifiers()
    {
        var valid = ShortcutApi.TryCreate([Control, Shift, Alt, P], out var shortcut, out var error);

        Assert.False(valid);
        Assert.Null(shortcut);
        Assert.Contains("two modifiers", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectDuplicateModifiersAfterNormalization()
    {
        var valid = ShortcutApi.TryCreate([LeftControl, Control, P], out var shortcut, out var error);

        Assert.False(valid);
        Assert.Null(shortcut);
        Assert.Contains("duplicate", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectMouseKeys()
    {
        var valid = ShortcutApi.TryCreate([Control, LeftMouseButton], out var shortcut, out var error);

        Assert.False(valid);
        Assert.Null(shortcut);
        Assert.Contains("keyboard", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CaptureRecordsModifiersThenCompletesOnMainKey()
    {
        var capture = ShortcutApi.CreateCaptureState();

        var first = ShortcutApi.RecordKey(capture, LeftControl);
        var second = ShortcutApi.RecordKey(capture, RightShift);
        var completed = ShortcutApi.RecordKey(capture, P);

        Assert.False(ShortcutApi.BoolProperty(first, "IsComplete"));
        Assert.False(ShortcutApi.BoolProperty(second, "IsComplete"));
        Assert.True(ShortcutApi.BoolProperty(completed, "IsComplete"));
        Assert.Equal("Ctrl+Shift+P", ShortcutApi.ObjectProperty(completed, "Shortcut")!.ToString());
        Assert.True(ShortcutApi.BoolProperty(capture, "IsComplete"));
    }

    [Fact]
    public void CaptureCancelsOnEscape()
    {
        var capture = ShortcutApi.CreateCaptureState();

        var result = ShortcutApi.RecordKey(capture, Escape);

        Assert.True(ShortcutApi.BoolProperty(result, "IsCanceled"));
        Assert.False(ShortcutApi.BoolProperty(result, "IsComplete"));
        Assert.True(ShortcutApi.BoolProperty(capture, "IsCanceled"));
    }

    [Theory]
    [MemberData(nameof(ClearKeys))]
    public void CaptureClearsOnBackspaceOrDelete(int clearKey)
    {
        var capture = ShortcutApi.CreateCaptureState();
        ShortcutApi.RecordKey(capture, Control);

        var result = ShortcutApi.RecordKey(capture, clearKey);

        Assert.True(ShortcutApi.BoolProperty(result, "IsCleared"));
        Assert.False(ShortcutApi.BoolProperty(result, "IsComplete"));
        Assert.Empty(ShortcutApi.PendingKeys(capture));
    }

    [Fact]
    public void CaptureReportsInvalidInputWithoutCompleting()
    {
        var capture = ShortcutApi.CreateCaptureState();

        var result = ShortcutApi.RecordKey(capture, P);

        Assert.False(ShortcutApi.BoolProperty(result, "IsComplete"));
        Assert.False(ShortcutApi.BoolProperty(capture, "IsComplete"));
        Assert.NotNull(ShortcutApi.ObjectProperty(result, "Error"));
    }

    [Fact]
    public void RecordingOrdersSameFrameKeysWithModifiersFirst()
    {
        var ordered = ShortcutApi.OrderNewKeysForRecording(
            currentKeyValues: [P, RightShift, LeftControl],
            previousKeyValues: []);

        Assert.Equal([Control, Shift, P], ordered);
    }

    [Fact]
    public void HotkeyServiceMatchesPressedShortcutKeysAfterNormalization()
    {
        var shortcut = ShortcutApi.Create(Control, P);

        Assert.True(ShortcutApi.IsPressed(shortcut, LeftControl, P));
        Assert.False(ShortcutApi.IsPressed(shortcut, LeftControl));
        Assert.False(ShortcutApi.IsPressed(shortcut, LeftControl, Shift, P));
    }

    [Fact]
    public void HotkeyServiceDoesNotTriggerWhenShortcutChangesWhilePressed()
    {
        var shortcut = ShortcutApi.Create(Control, P);

        Assert.False(ShortcutApi.ShouldTrigger(
            currentShortcut: shortcut,
            previousShortcut: null,
            pressed: true,
            wasPressed: false,
            canTrigger: true));
    }

    [Fact]
    public void HotkeyServiceTriggersAfterSameShortcutWasReleasedAndPressedAgain()
    {
        var shortcut = ShortcutApi.Create(Control, P);
        var sameShortcut = ShortcutApi.Create(LeftControl, P);

        Assert.True(ShortcutApi.ShouldTrigger(
            currentShortcut: shortcut,
            previousShortcut: sameShortcut,
            pressed: true,
            wasPressed: false,
            canTrigger: true));
    }

    public static TheoryData<int> ClearKeys => new()
    {
        Backspace,
        Delete,
    };

    private static class ShortcutApi
    {
        private static readonly Type ValidatorType = RequiredType("BazookaLens.UI.KeyboardShortcutValidator");
        private static readonly Type ShortcutType = RequiredType("BazookaLens.UI.KeyboardShortcut");
        private static readonly Type CaptureStateType = RequiredType("BazookaLens.UI.ShortcutCaptureState");
        private static readonly Type HotkeyServiceType = RequiredType("BazookaLens.UI.HotkeyService");

        public static object Create(params int[] keyValues)
        {
            var valid = TryCreate(keyValues, out var shortcut, out var error);
            Assert.True(valid, error);
            return shortcut!;
        }

        public static bool TryCreate(int[] keyValues, out object? shortcut, out string? error)
        {
            var method = ValidatorType.GetMethod("TryCreate")!;
            var args = new object?[] { KeyArray(keyValues), null, null };
            var valid = (bool)method.Invoke(null, args)!;
            shortcut = args[1];
            error = (string?)args[2];
            return valid;
        }

        public static string Format(object? shortcut)
        {
            var method = ShortcutType.GetMethod("Format")!;
            return (string)method.Invoke(null, [shortcut])!;
        }

        public static int[] Keys(object shortcut)
        {
            var values = (System.Collections.IEnumerable)ShortcutType.GetProperty("Keys")!.GetValue(shortcut)!;
            return values.Cast<object>().Select(Convert.ToInt32).ToArray();
        }

        public static object CreateCaptureState() => Activator.CreateInstance(CaptureStateType)!;

        public static object RecordKey(object capture, int keyValue)
        {
            var method = CaptureStateType.GetMethod("RecordKey")!;
            return method.Invoke(capture, [Key(keyValue)])!;
        }

        public static IReadOnlyList<int> PendingKeys(object capture)
        {
            var values = (System.Collections.IEnumerable)CaptureStateType.GetProperty("PendingKeys")!.GetValue(capture)!;
            return values.Cast<object>().Select(Convert.ToInt32).ToArray();
        }

        public static int[] OrderNewKeysForRecording(int[] currentKeyValues, int[] previousKeyValues)
        {
            var method = CaptureStateType.GetMethod("OrderNewKeysForRecording")!;
            var values = (System.Collections.IEnumerable)method.Invoke(null, [KeyArray(currentKeyValues), KeyArray(previousKeyValues)])!;
            return values.Cast<object>().Select(Convert.ToInt32).ToArray();
        }

        public static bool IsPressed(object shortcut, params int[] keyValues)
        {
            var method = HotkeyServiceType.GetMethod("IsPressed")!;
            return (bool)method.Invoke(null, [shortcut, KeyArray(keyValues)])!;
        }

        public static bool ShouldTrigger(
            object? currentShortcut,
            object? previousShortcut,
            bool pressed,
            bool wasPressed,
            bool canTrigger)
        {
            var method = HotkeyServiceType.GetMethod("ShouldTrigger")!;
            return (bool)method.Invoke(null, [currentShortcut, previousShortcut, pressed, wasPressed, canTrigger])!;
        }

        public static bool BoolProperty(object target, string propertyName)
            => (bool)ObjectProperty(target, propertyName)!;

        public static object? ObjectProperty(object target, string propertyName)
            => target.GetType().GetProperty(propertyName)!.GetValue(target);

        private static object KeyArray(int[] keyValues)
        {
            var keyType = ValidatorType.GetMethod("TryCreate")!.GetParameters()[0].ParameterType.GetGenericArguments()[0];
            var array = Array.CreateInstance(keyType, keyValues.Length);
            for (var i = 0; i < keyValues.Length; i++)
                array.SetValue(Enum.ToObject(keyType, keyValues[i]), i);

            return array;
        }

        private static object Key(int keyValue)
        {
            var keyType = CaptureStateType.GetMethod("RecordKey")!.GetParameters()[0].ParameterType;
            return Enum.ToObject(keyType, keyValue);
        }

        private static Type RequiredType(string typeName)
            => typeof(Plugin).Assembly.GetType(typeName, throwOnError: true)!;
    }
}
