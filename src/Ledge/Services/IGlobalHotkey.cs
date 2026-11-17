using System.Windows.Input;

namespace Ledge.Services;

/// <summary>
/// The one system-wide shortcut that opens Ledge. An interface so the
/// Settings ViewModel can be tested without registering real hotkeys.
/// </summary>
public interface IGlobalHotkey
{
    /// <summary>The registered shortcut, or null if none is active.</summary>
    KeyGesture? Current { get; }

    /// <summary>
    /// Switches to <paramref name="gesture"/>. If another app owns it, returns
    /// false and the previous shortcut stays active.
    /// </summary>
    bool TryChange(KeyGesture gesture);

    /// <summary>
    /// Turns the shortcut off for a moment (while Settings records a new one),
    /// so pressing it reaches the Settings window instead of toggling the panel.
    /// </summary>
    void Suspend();

    /// <summary>Turns the current shortcut back on after <see cref="Suspend"/>.</summary>
    void Resume();
}
