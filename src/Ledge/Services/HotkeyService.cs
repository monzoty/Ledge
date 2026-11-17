using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using Ledge.Interop;

namespace Ledge.Services;

/// <summary>
/// Registers one global hotkey and raises <see cref="Pressed"/> when it fires,
/// whichever app has focus. Owns a hidden message-only window that receives
/// WM_HOTKEY, so it doesn't depend on any visible window.
/// </summary>
public sealed class HotkeyService : IGlobalHotkey, IDisposable
{
    /// <summary>Our own ID for the hotkey; WM_HOTKEY reports it back in wParam.</summary>
    private const int HotkeyId = 1;

    private readonly HwndSource _messageWindow;
    private bool _isRegistered;
    private bool _isSuspended;

    public HotkeyService()
    {
        // A message-only window: it can receive messages but is never shown,
        // has no taskbar entry, and isn't enumerated with other windows.
        // https://learn.microsoft.com/windows/win32/winmsg/window-features#message-only-windows
        var parameters = new HwndSourceParameters("Ledge.HotkeySink")
        {
            ParentWindow = NativeMethods.HwndMessage,
            WindowStyle = 0,
        };

        _messageWindow = new HwndSource(parameters);

        // AddHook lets us see every Win32 message sent to that window.
        // https://learn.microsoft.com/dotnet/api/system.windows.interop.hwndsource.addhook
        _messageWindow.AddHook(WndProc);
    }

    /// <summary>Raised on the UI thread when the hotkey is pressed.</summary>
    public event EventHandler? Pressed;

    /// <inheritdoc />
    public KeyGesture? Current { get; private set; }

    /// <summary>
    /// Registers the hotkey at start-up. Returns false if another app already
    /// uses it, so the caller can tell the user; throws for any other failure.
    /// </summary>
    public bool TryRegister(KeyGesture gesture)
    {
        Unregister();

        if (!Register(gesture))
        {
            Current = null;
            return false;
        }

        Current = gesture;
        return true;
    }

    /// <inheritdoc />
    public bool TryChange(KeyGesture gesture)
    {
        var previous = Current;
        Unregister();

        if (RegisterOrFalse(gesture))
        {
            Current = gesture;
            _isSuspended = false;
            return true;
        }

        // Taken by another app: put the old one back. (It was ours a moment
        // ago, so registering it again succeeds.)
        if (previous is not null && !_isSuspended)
        {
            Register(previous);
        }

        return false;
    }

    /// <inheritdoc />
    public void Suspend()
    {
        Unregister();
        _isSuspended = true;
    }

    /// <inheritdoc />
    public void Resume()
    {
        if (!_isSuspended)
        {
            return;
        }

        _isSuspended = false;
        if (Current is not null)
        {
            Register(Current);
        }
    }

    public void Dispose()
    {
        Unregister();
        _messageWindow.RemoveHook(WndProc);
        _messageWindow.Dispose();
    }

    private static uint ToNativeModifiers(ModifierKeys modifiers)
    {
        uint result = 0;

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            result |= NativeMethods.ModAlt;
        }

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            result |= NativeMethods.ModControl;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            result |= NativeMethods.ModShift;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            result |= NativeMethods.ModWin;
        }

        return result;
    }

    /// <summary>
    /// RegisterHotKey for <paramref name="gesture"/>. False only when another
    /// app already owns that combination; any other failure throws.
    /// https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-registerhotkey
    /// </summary>
    private bool Register(KeyGesture gesture)
    {
        var modifiers = ToNativeModifiers(gesture.Modifiers) | NativeMethods.ModNoRepeat;
        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(gesture.Key);

        if (NativeMethods.RegisterHotKey(_messageWindow.Handle, HotkeyId, modifiers, virtualKey))
        {
            _isRegistered = true;
            return true;
        }

        var error = Marshal.GetLastPInvokeError();
        if (error == NativeMethods.ErrorHotkeyAlreadyRegistered)
        {
            return false;
        }

        throw new Win32Exception(error);
    }

    /// <summary>
    /// For shortcuts picked in Settings: Windows may refuse a combination it
    /// reserves for itself with other error codes; to the user that's simply
    /// "can't use this one", so report false instead of throwing.
    /// </summary>
    private bool RegisterOrFalse(KeyGesture gesture)
    {
        try
        {
            return Register(gesture);
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private void Unregister()
    {
        if (!_isRegistered)
        {
            return;
        }

        NativeMethods.UnregisterHotKey(_messageWindow.Handle, HotkeyId);
        _isRegistered = false;
    }

    /// <summary>
    /// Called for every message the hidden window receives. We only care about
    /// WM_HOTKEY with our ID; everything else is left for default handling.
    /// </summary>
    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeMethods.WmHotkey && wParam == HotkeyId)
        {
            handled = true;
            Pressed?.Invoke(this, EventArgs.Empty);
        }

        return 0;
    }
}
