using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;

namespace Ledge.Interop;

/// <summary>
/// Reads and sets window positions in physical pixels. Working in pixels end
/// to end avoids DIP conversion errors when the scaling level changes at runtime.
/// </summary>
internal static class WindowPlacement
{
    /// <summary>The primary monitor's work area (screen minus taskbar), in physical pixels.</summary>
    public static Int32Rect GetPrimaryWorkArea()
    {
        var monitor = NativeMethods.MonitorFromPoint(new NativeMethods.Point(0, 0), NativeMethods.MonitorDefaultToPrimary);
        var info = new NativeMethods.MonitorInfo { Size = (uint)Marshal.SizeOf<NativeMethods.MonitorInfo>() };

        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        return new Int32Rect(info.Work.Left, info.Work.Top, info.Work.Width, info.Work.Height);
    }

    /// <summary>A window's current size in physical pixels.</summary>
    public static (int Width, int Height) GetSize(nint hwnd)
    {
        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        return (rect.Width, rect.Height);
    }

    /// <summary>Moves a window without resizing, re-ordering or focusing it.</summary>
    public static void MoveTo(nint hwnd, int x, int y)
    {
        const uint flags = NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate;

        if (!NativeMethods.SetWindowPos(hwnd, 0, x, y, 0, 0, flags))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }
}
