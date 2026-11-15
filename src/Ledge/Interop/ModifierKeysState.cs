namespace Ledge.Interop;

/// <summary>
/// Reads the physical state of the modifier keys, whichever app has focus.
/// </summary>
internal static class ModifierKeysState
{
    private static readonly int[] ModifierKeys =
    [
        NativeMethods.VkShift,
        NativeMethods.VkControl,
        NativeMethods.VkMenu,
        NativeMethods.VkLWin,
        NativeMethods.VkRWin,
    ];

    /// <summary>True while any of Shift, Ctrl, Alt or Win is held down.</summary>
    public static bool AnyDown()
    {
        foreach (var key in ModifierKeys)
        {
            // The high bit (sign bit of the short) means "currently down".
            if (NativeMethods.GetAsyncKeyState(key) < 0)
            {
                return true;
            }
        }

        return false;
    }
}
