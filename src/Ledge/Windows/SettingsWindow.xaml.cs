using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Ledge.Interop;
using Ledge.ViewModels;

namespace Ledge.Windows;

/// <summary>
/// The Settings window. Code-behind only handles what the ViewModel can't:
/// focus, turning raw key presses into "key + modifiers", and the dark title bar.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        // Closing while recording must switch the old shortcut back on.
        Closed += (_, _) => _viewModel.CancelRecordingCommand.Execute(null);

        // A click on a label or empty space doesn't move keyboard focus, so the
        // box wouldn't notice it; cancel recording for any click outside the box.
        PreviewMouseDown += (_, _) =>
        {
            if (_viewModel.IsRecording && !ShortcutBox.IsMouseOver)
            {
                _viewModel.CancelRecordingCommand.Execute(null);
            }
        };
    }

    /// <summary>The HWND exists now: ask Windows for a dark title bar (DESIGN.md §5).</summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var useDark = 1;
        NativeMethods.DwmSetWindowAttribute(
            new WindowInteropHelper(this).Handle,
            NativeMethods.DwmwaUseImmersiveDarkMode,
            ref useDark,
            sizeof(int));
    }

    private void OnShortcutBoxMouseDown(object sender, MouseButtonEventArgs e)
    {
        Keyboard.Focus(ShortcutBox);
        _viewModel.StartRecordingCommand.Execute(null);
        e.Handled = true;
    }

    /// <summary>
    /// Every key in the box goes to the ViewModel while recording, and is
    /// "handled" so it doesn't also do its normal job (Alt opening the window
    /// menu, Tab moving focus, Space clicking something).
    /// </summary>
    private void OnShortcutBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_viewModel.IsRecording)
        {
            // Keyboard users: Tab to the box, then Enter or Space starts recording.
            if (e.Key is Key.Enter or Key.Space)
            {
                e.Handled = true;
                _viewModel.StartRecordingCommand.Execute(null);
            }

            return;
        }

        e.Handled = true;
        _viewModel.CaptureShortcut(RealKey(e), Keyboard.Modifiers);
    }

    /// <summary>Clicking elsewhere (or Tab away) cancels recording (DESIGN.md §5).</summary>
    private void OnShortcutBoxLostFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        _viewModel.CancelRecordingCommand.Execute(null);

    /// <summary>
    /// WPF reports some keys indirectly: with Alt held, e.Key is "System" and
    /// the actual key is in SystemKey; with an input method (IME) or a dead key
    /// (e.g. ´ on some layouts) it's in other properties.
    /// https://learn.microsoft.com/dotnet/api/system.windows.input.keyeventargs.systemkey
    /// </summary>
    private static Key RealKey(KeyEventArgs e) => e.Key switch
    {
        Key.System => e.SystemKey,
        Key.ImeProcessed => e.ImeProcessedKey,
        Key.DeadCharProcessed => e.DeadCharProcessedKey,
        _ => e.Key,
    };
}
