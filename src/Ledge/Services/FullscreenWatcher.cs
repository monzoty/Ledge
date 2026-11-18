using System.Runtime.InteropServices;
using System.Windows.Interop;
using Ledge.Interop;

namespace Ledge.Services;

/// <summary>
/// Knows whether a full-screen app (video, game, slideshow) is in front, so
/// the pill can step out of the way (PRD F1.6, DESIGN.md §5 "Full-screen apps").
/// </summary>
/// <remarks>
/// The trick: the taskbar is an "app bar", and Windows tells every registered
/// app bar when a full-screen app opens or closes (ABN_FULLSCREENAPP). That's
/// how the taskbar knows to hide under videos. We register a hidden app bar
/// that reserves NO screen space, purely to receive that message. No polling,
/// and it also catches full screen inside the same window (a browser video),
/// where the foreground window doesn't change.
/// https://learn.microsoft.com/windows/win32/shell/application-desktop-toolbars
/// </remarks>
public sealed class FullscreenWatcher : IDisposable
{
    private readonly HwndSource _window;
    private readonly uint _callbackMessage;
    private readonly uint _taskbarCreatedMessage;
    private bool _isRegistered;

    public FullscreenWatcher()
    {
        // A hidden, zero-size popup window. A normal (not message-only) window,
        // because it must also hear the "TaskbarCreated" broadcast, which is
        // only sent to top-level windows.
        var parameters = new HwndSourceParameters("Ledge.FullscreenWatcher")
        {
            WindowStyle = unchecked((int)WsPopup),
            ExtendedWindowStyle = (int)NativeMethods.WsExToolWindow,
            Width = 0,
            Height = 0,
        };
        _window = new HwndSource(parameters);
        _window.AddHook(WndProc);

        _callbackMessage = NativeMethods.RegisterWindowMessage("Ledge.AppBarCallback");
        _taskbarCreatedMessage = NativeMethods.RegisterWindowMessage("TaskbarCreated");
    }

    /// <summary>A full-screen app came to the front or went away. Raised on the UI thread.</summary>
    public event EventHandler? Changed;

    public bool IsFullscreenAppInFront { get; private set; }

    /// <summary>Starts listening, and reads the current state once.</summary>
    public void Start()
    {
        Register();

        if (NativeMethods.SHQueryUserNotificationState(out var state) == 0)
        {
            Set(state is NativeMethods.QunsBusy or NativeMethods.QunsRunningD3dFullScreen or NativeMethods.QunsPresentationMode);
        }
    }

    public void Dispose()
    {
        Unregister();
        _window.RemoveHook(WndProc);
        _window.Dispose();
    }

    private const uint WsPopup = 0x80000000; // WS_POPUP

    private void Register()
    {
        var data = NewData();
        data.UCallbackMessage = _callbackMessage;
        NativeMethods.SHAppBarMessage(NativeMethods.AbmNew, ref data);
        _isRegistered = true;
    }

    private void Unregister()
    {
        if (!_isRegistered)
        {
            return;
        }

        var data = NewData();
        NativeMethods.SHAppBarMessage(NativeMethods.AbmRemove, ref data);
        _isRegistered = false;
    }

    private NativeMethods.AppBarData NewData() => new()
    {
        CbSize = (uint)Marshal.SizeOf<NativeMethods.AppBarData>(),
        HWnd = _window.Handle,
    };

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == _callbackMessage && wParam == NativeMethods.AbnFullscreenApp)
        {
            Set(lParam != 0);
            handled = true;
        }
        else if (msg == _taskbarCreatedMessage)
        {
            // explorer.exe restarted and forgot every app bar: sign up again.
            _isRegistered = false;
            Register();
        }

        return 0;
    }

    private void Set(bool fullscreen)
    {
        if (fullscreen == IsFullscreenAppInFront)
        {
            return;
        }

        IsFullscreenAppInFront = fullscreen;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
