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

    // --- Global hotkey -----------------------------------------------------

    /// <summary>Message sent to the registering window when the hotkey is pressed.</summary>
    internal const int WmHotkey = 0x0312; // WM_HOTKEY

    /// <summary>Parent value that creates a message-only window (never visible).</summary>
    internal const nint HwndMessage = -3; // HWND_MESSAGE

    /// <summary>GetLastError value when another app already owns the key combination.</summary>
    internal const int ErrorHotkeyAlreadyRegistered = 1409; // ERROR_HOTKEY_ALREADY_REGISTERED

    // Modifier flags for RegisterHotKey.
    internal const uint ModAlt = 0x0001;      // MOD_ALT
    internal const uint ModControl = 0x0002;  // MOD_CONTROL
    internal const uint ModShift = 0x0004;    // MOD_SHIFT
    internal const uint ModWin = 0x0008;      // MOD_WIN
    internal const uint ModNoRepeat = 0x4000; // MOD_NOREPEAT: holding the keys fires once, not repeatedly

    /// <summary>
    /// Asks Windows to post WM_HOTKEY to hWnd whenever the key combination is
    /// pressed, no matter which app has focus. Fails if another app owns it.
    /// https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-registerhotkey
    /// </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    /// <summary>
    /// Releases a hotkey registered with RegisterHotKey.
    /// https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-unregisterhotkey
    /// </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(nint hWnd, int id);

    // --- Foreground (focus) ------------------------------------------------

    /// <summary>
    /// Returns the window the user is currently working in (receives keyboard input).
    /// https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getforegroundwindow
    /// </summary>
    [LibraryImport("user32.dll")]
    internal static partial nint GetForegroundWindow();

    /// <summary>
    /// Brings a window to the foreground. Windows only allows this for the
    /// process the user last interacted with (a foreground "lock" stops pop-ups
    /// stealing focus); we call it while Ledge is still in the foreground.
    /// https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setforegroundwindow
    /// </summary>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetForegroundWindow(nint hWnd);

    /// <summary>"Any process" for AllowSetForegroundWindow.</summary>
    internal const int AsfwAny = -1; // ASFW_ANY

    /// <summary>
    /// Lets another process take the foreground. A second copy of Ledge (which
    /// the user just launched, so it may take the foreground) calls this before
    /// telling the first copy to open its panel; without it, Windows' foreground
    /// lock would stop the first copy from activating itself.
    /// https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-allowsetforegroundwindow
    /// </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AllowSetForegroundWindow(int processId);

    /// <summary>
    /// True if the handle still refers to an existing window (the app may have closed meanwhile).
    /// https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-iswindow
    /// </summary>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindow(nint hWnd);

    // --- Keyboard state ----------------------------------------------------

    // Virtual-key codes. https://learn.microsoft.com/windows/win32/inputdev/virtual-key-codes
    internal const int VkShift = 0x10;   // VK_SHIFT
    internal const int VkControl = 0x11; // VK_CONTROL
    internal const int VkMenu = 0x12;    // VK_MENU (Alt)
    internal const int VkLWin = 0x5B;    // VK_LWIN
    internal const int VkRWin = 0x5C;    // VK_RWIN

    /// <summary>
    /// Whether a key is physically down right now (high bit set = down),
    /// regardless of which app has focus.
    /// https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getasynckeystate
    /// </summary>
    [LibraryImport("user32.dll")]
    internal static partial short GetAsyncKeyState(int vKey);

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
