namespace Ledge.Interop;

/// <summary>
/// Remembers which app the user was in and hands focus back to it later.
/// </summary>
internal static class ForegroundWindow
{
    /// <summary>
    /// The window that currently has focus, or 0 if it is <paramref name="ownWindow"/>
    /// (we never want to "return" focus to ourselves).
    /// </summary>
    public static nint Capture(nint ownWindow)
    {
        var foreground = NativeMethods.GetForegroundWindow();
        return foreground == ownWindow ? 0 : foreground;
    }

    /// <summary>
    /// Gives focus back to a window captured earlier, if it still exists.
    /// Must be called while our window is still the foreground window,
    /// otherwise Windows' foreground lock silently refuses.
    /// </summary>
    public static void Restore(nint window)
    {
        if (window != 0 && NativeMethods.IsWindow(window))
        {
            NativeMethods.SetForegroundWindow(window);
        }
    }
}
