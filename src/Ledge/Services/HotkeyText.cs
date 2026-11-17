using System.Globalization;
using System.Windows.Input;

namespace Ledge.Services;

/// <summary>
/// Converts between shortcut text and <see cref="KeyGesture"/>, and decides
/// which shortcuts may be used as Ledge's global hotkey (DESIGN.md §5).
/// </summary>
/// <remarks>
/// Two text forms:
/// <list type="bullet">
/// <item>Stored (settings.json): "Win+Alt+L", which WPF's KeyGestureConverter can read back.</item>
/// <item>Shown (Settings window): "Win + Alt + L", with digits shown as "1" instead of "D1".</item>
/// </list>
/// Modifiers are always written in the same order: Win, Ctrl, Alt, Shift.
/// </remarks>
public static class HotkeyText
{
    private const string StoredSeparator = "+";
    private const string ShownSeparator = " + ";

    private static readonly (ModifierKeys Flag, string Name)[] ModifierNames =
    [
        (ModifierKeys.Windows, "Win"),
        (ModifierKeys.Control, "Ctrl"),
        (ModifierKeys.Alt, "Alt"),
        (ModifierKeys.Shift, "Shift"),
    ];

    /// <summary>
    /// "Win+Alt+L" → a KeyGesture, or null if the text isn't a valid shortcut.
    /// WPF's converter throws for unknown keys, and KeyGesture itself refuses
    /// combinations it can't use (e.g. a letter with no modifier).
    /// https://learn.microsoft.com/dotnet/api/system.windows.input.keygestureconverter
    /// </summary>
    public static KeyGesture? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return new KeyGestureConverter().ConvertFromInvariantString(text) as KeyGesture;
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Text for settings.json, e.g. "Win+Alt+L".</summary>
    public static string ToStored(KeyGesture gesture) =>
        Join(gesture, StoredSeparator, KeyName(gesture.Key));

    /// <summary>Text for people, e.g. "Win + Alt + L" or "Ctrl + Alt + 1".</summary>
    public static string ToShown(KeyGesture gesture) =>
        Join(gesture, ShownSeparator, ShownKeyName(gesture.Key));

    /// <summary>
    /// DESIGN.md §5: the shortcut must include Win, or Ctrl and Alt together,
    /// so it can't steal app shortcuts like Ctrl+L or menu keys like Alt+F.
    /// The key itself must not be a modifier.
    /// </summary>
    public static bool IsAllowed(ModifierKeys modifiers, Key key)
    {
        if (IsModifierKey(key) || key == Key.None)
        {
            return false;
        }

        var hasWin = modifiers.HasFlag(ModifierKeys.Windows);
        var hasCtrlAlt = modifiers.HasFlag(ModifierKeys.Control) && modifiers.HasFlag(ModifierKeys.Alt);
        return hasWin || hasCtrlAlt;
    }

    /// <summary>True for keys that only modify others (Ctrl, Alt, Shift, Win).</summary>
    public static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or
        Key.LWin or Key.RWin or
        Key.System;

    private static string Join(KeyGesture gesture, string separator, string keyName)
    {
        var parts = ModifierNames
            .Where(m => gesture.Modifiers.HasFlag(m.Flag))
            .Select(m => m.Name)
            .Append(keyName);
        return string.Join(separator, parts);
    }

    private static string KeyName(Key key) =>
        new KeyConverter().ConvertToString(null, CultureInfo.InvariantCulture, key) ?? key.ToString();

    /// <summary>D0–D9 (the number row) read better as plain digits.</summary>
    private static string ShownKeyName(Key key) =>
        key is >= Key.D0 and <= Key.D9
            ? ((int)(key - Key.D0)).ToString(CultureInfo.InvariantCulture)
            : KeyName(key);
}
