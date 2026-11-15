using System.Runtime.InteropServices;

namespace Ledge.Interop;

/// <summary>
/// Every P/Invoke declaration in the app lives here (CLAUDE.md).
/// We use [LibraryImport], which generates the marshalling code at compile
/// time (faster, trimming-safe) instead of the older [DllImport].
/// Docs: https://learn.microsoft.com/dotnet/standard/native-interop/pinvoke-source-generation
/// </summary>
internal static partial class NativeMethods
{
    // --- Window long values ------------------------------------------------

    /// <summary>Index for the extended window style in Get/SetWindowLongPtr.</summary>
    internal const int GwlExStyle = -20; // GWL_EXSTYLE

    /// <summary>
    /// Tool window: not shown in Alt+Tab or the taskbar.
    /// https://learn.microsoft.com/windows/win32/winmsg/extended-window-styles
    /// </summary>
    internal const nint WsExToolWindow = 0x00000080; // WS_EX_TOOLWINDOW

    /// <summary>
    /// Clicking the window does not make it the foreground (focused) window.
    /// https://learn.microsoft.com/windows/win32/winmsg/extended-window-styles
    /// </summary>
    internal const nint WsExNoActivate = 0x08000000; // WS_EX_NOACTIVATE

    /// <summary>
    /// Reads a value (here: the extended style flags) from a window. 64-bit only;
    /// in 32-bit processes user32 exports GetWindowLongW instead.
    /// https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getwindowlongptrw
    /// </summary>
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static partial nint GetWindowLongPtr64(nint hWnd, int nIndex);

    /// <summary>
    /// 32-bit version of GetWindowLongPtr.
    /// https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getwindowlongw
    /// </summary>
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    internal static partial int GetWindowLong32(nint hWnd, int nIndex);

    /// <summary>
    /// Writes a value (here: the extended style flags) to a window. Returns the
    /// previous value, or 0 on failure. 64-bit only.
    /// https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowlongptrw
    /// </summary>
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static partial nint SetWindowLongPtr64(nint hWnd, int nIndex, nint dwNewLong);

    /// <summary>
    /// 32-bit version of SetWindowLongPtr.
    /// https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowlongw
    /// </summary>
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    internal static partial int SetWindowLong32(nint hWnd, int nIndex, int dwNewLong);

    // --- Monitors ----------------------------------------------------------

    /// <summary>MonitorFromPoint flag: if the point is on no monitor, return the primary one.</summary>
    internal const uint MonitorDefaultToPrimary = 0x00000001; // MONITOR_DEFAULTTOPRIMARY

    /// <summary>
    /// Returns the monitor that contains a point. (0,0) is always on the primary monitor.
    /// https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-monitorfrompoint
    /// </summary>
    [LibraryImport("user32.dll")]
    internal static partial nint MonitorFromPoint(Point pt, uint dwFlags);

    /// <summary>
    /// Gets a monitor's full bounds and its work area (minus the taskbar), in physical pixels.
    /// https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getmonitorinfow
    /// </summary>
    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetMonitorInfo(nint hMonitor, ref MonitorInfo lpmi);

    // --- Window position ---------------------------------------------------

    /// <summary>SetWindowPos flag: keep the current size (ignore cx, cy).</summary>
    internal const uint SwpNoSize = 0x0001; // SWP_NOSIZE

    /// <summary>SetWindowPos flag: keep the current z-order (ignore hWndInsertAfter).</summary>
    internal const uint SwpNoZOrder = 0x0004; // SWP_NOZORDER

    /// <summary>SetWindowPos flag: don't activate (focus) the window.</summary>
    internal const uint SwpNoActivate = 0x0010; // SWP_NOACTIVATE

    /// <summary>
    /// Gets a window's bounds in physical pixels (screen coordinates).
    /// https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getwindowrect
    /// </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(nint hWnd, out Rect lpRect);

    /// <summary>
    /// Moves, resizes or re-orders a window, in physical pixels.
    /// https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowpos
    /// </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    // --- Structs (memory layout must match the Win32 definitions) ----------

    /// <summary>Win32 POINT. https://learn.microsoft.com/windows/win32/api/windef/ns-windef-point</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Point(int x, int y)
    {
        public int X = x;
        public int Y = y;
    }

    /// <summary>Win32 RECT: edges, not width/height. https://learn.microsoft.com/windows/win32/api/windef/ns-windef-rect</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly int Width => Right - Left;

        public readonly int Height => Bottom - Top;
    }

    /// <summary>
    /// Win32 MONITORINFO. Size must be set to the struct's size before calling GetMonitorInfo.
    /// https://learn.microsoft.com/windows/win32/api/winuser/ns-winuser-monitorinfo
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MonitorInfo
    {
        public uint Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }
}
