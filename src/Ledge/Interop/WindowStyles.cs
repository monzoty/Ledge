using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Ledge.Interop;

/// <summary>
/// Friendly helpers around the raw window-style P/Invokes in <see cref="NativeMethods"/>.
/// </summary>
internal static class WindowStyles
{
    /// <summary>
    /// Makes a window behave like an overlay:
    /// hidden from Alt+Tab (WS_EX_TOOLWINDOW) and never takes focus when
    /// clicked (WS_EX_NOACTIVATE). Call before the window is first shown,
    /// e.g. from OnSourceInitialized, so no SetWindowPos refresh is needed.
    /// </summary>
    public static void ApplyOverlayStyles(nint hwnd)
    {
        var exStyle = GetExStyle(hwnd);
        SetExStyle(hwnd, exStyle | NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate);
    }

    private static nint GetExStyle(nint hwnd)
    {
        return Environment.Is64BitProcess
            ? NativeMethods.GetWindowLongPtr64(hwnd, NativeMethods.GwlExStyle)
            : NativeMethods.GetWindowLong32(hwnd, NativeMethods.GwlExStyle);
    }

    private static void SetExStyle(nint hwnd, nint value)
    {
        // SetWindowLongPtr returns the *previous* value, and 0 on failure — but 0 can
        // also be a legitimate previous value. The documented way to tell them apart:
        // clear the last error first, then check it after the call.
        Marshal.SetLastPInvokeError(0);

        var previous = Environment.Is64BitProcess
            ? NativeMethods.SetWindowLongPtr64(hwnd, NativeMethods.GwlExStyle, value)
            : NativeMethods.SetWindowLong32(hwnd, NativeMethods.GwlExStyle, (int)value);

        var error = Marshal.GetLastPInvokeError();
        if (previous == 0 && error != 0)
        {
            throw new Win32Exception(error);
        }
    }
}
