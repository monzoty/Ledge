using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Ledge.Core.Models;
using Ledge.Core.Services;
using Ledge.Interop;
using Ledge.Services;
using Ledge.ViewModels;
using Microsoft.Extensions.Logging;

namespace Ledge.Windows;

/// <summary>
/// The overlay window. Code-behind holds only visual concerns: placement,
/// hover open/close, focus hand-off, and (later) animations.
/// In M3 the "is it expanded?" state moves to PillViewModel.
/// </summary>
/// <remarks>
/// Two ways to be open (PRD F2):
/// <list type="bullet">
/// <item>Hover: shown, but NOT focused. Closes when the mouse leaves.</item>
/// <item>Hotkey / click: shown AND focused (cursor in quick-add). Closes on
/// Esc, hotkey, or clicking elsewhere; focus goes back to the previous app.</item>
/// </list>
/// </remarks>
public sealed partial class PillWindow : Window
{
    // Injected by the Generic Host (App.BuildHost). The host also disposes them on exit.
    private readonly ScreenService _screen;
    private readonly HotkeyService _hotkey;
    private readonly TaskListViewModel _tasks;

    // Hover delays (DESIGN.md §5) stop the panel flickering when the mouse just passes by.
    // DispatcherTimer ticks on the UI thread, so it can touch WPF elements directly.
    // https://learn.microsoft.com/dotnet/api/system.windows.threading.dispatchertimer
    private readonly DispatcherTimer _openTimer;
    private readonly DispatcherTimer _closeTimer;

    /// <summary>How often we check whether the hotkey's modifiers are released.</summary>
    private static readonly TimeSpan ReturnFocusPollInterval = TimeSpan.FromMilliseconds(20);

    /// <summary>Give up waiting after this long (e.g. a key is stuck) and hand focus back anyway.</summary>
    private static readonly TimeSpan ReturnFocusMaxWait = TimeSpan.FromSeconds(2);

    /// <summary>
    /// A tray click this soon after the panel closed because it lost focus is the
    /// SAME click: pressing the tray icon activates the taskbar first, which closes
    /// an open panel, and only then does the click arrive. Without this the click
    /// would immediately re-open what the user meant to close.
    /// </summary>
    private static readonly TimeSpan TrayClickGrace = TimeSpan.FromMilliseconds(500);

    private readonly TimeProvider _time;
    private readonly SettingsService _settings;
    private readonly ILogger<PillWindow> _log;
    private readonly TimerViewModel _timer;
    private readonly PillViewModel _pill;

    /// <summary>When the panel last closed because another window was activated.</summary>
    private long _closedByDeactivationAt;

    // Waits for Win/Alt/Ctrl/Shift to be released before handing focus back.
    private readonly DispatcherTimer _returnFocusTimer;

    /// <summary>The app the user was in before we took focus; 0 if none.</summary>
    private nint _previousForeground;

    /// <summary>The app to hand focus back to once modifiers are up; 0 if nothing pending.</summary>
    private nint _pendingFocusReturn;
    private int _returnFocusPolls;

    /// <summary>
    /// The DI container calls this constructor and passes in what it lists,
    /// so the window never decides for itself which services it gets.
    /// </summary>
    public PillWindow(
        ScreenService screen,
        HotkeyService hotkey,
        TaskListViewModel tasks,
        TimeProvider time,
        SettingsService settings,
        ThemeService theme,
        TimerViewModel timer,
        PillViewModel pill,
        ILogger<PillWindow> log)
    {
        _log = log;
        _timer = timer;
        _screen = screen;
        _hotkey = hotkey;
        _time = time;
        _settings = settings;

        InitializeComponent();
        InitializeAnimation();

        // Bindings inside the panel ({Binding QuickAddText}, …) look things up here.
        // https://learn.microsoft.com/dotnet/desktop/wpf/data/how-to-specify-the-binding-source
        PanelContent.DataContext = tasks;

        // A task completed yesterday must disappear today even if nothing else
        // changed (PRD F3). Re-reading the lists on every open is cheap and covers
        // the panel being opened after midnight. (A live midnight timer comes in 3c.)
        _tasks = tasks;

        // The undo hint sits outside the scrolling list, so the list's SizeChanged
        // doesn't see it appear or disappear; resize the panel for it too.
        UndoHint.IsVisibleChanged += (_, _) => Dispatcher.BeginInvoke(ResizeToContentIfOpen, DispatcherPriority.Loaded);
        DataNotice.IsVisibleChanged += (_, _) => Dispatcher.BeginInvoke(ResizeToContentIfOpen, DispatcherPriority.Loaded);
        FocusDoneLine.IsVisibleChanged += (_, _) => Dispatcher.BeginInvoke(ResizeToContentIfOpen, DispatcherPriority.Loaded);

        // The collapsed pill: "All clear", "n today" or the timer, and its width.
        _pill = pill;
        PillContent.DataContext = pill;
        pill.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PillViewModel.Mode))
            {
                AnimatePillWidth();
            }
        };

        // The timer row has its own ViewModel; the question it can show
        // ("Stop the current session…?") changes the panel's height.
        TimerRow.DataContext = timer;
        TimerRow.SizeChanged += (_, e) =>
        {
            if (e.HeightChanged)
            {
                ResizeToContentIfOpen();
            }
        };

        _returnFocusTimer = new DispatcherTimer { Interval = ReturnFocusPollInterval };
        _returnFocusTimer.Tick += (_, _) => TryReturnFocus();

        _openTimer = new DispatcherTimer { Interval = (TimeSpan)FindResource("HoverOpenDelay") };
        _openTimer.Tick += (_, _) =>
        {
            _openTimer.Stop();
            Expand();
        };

        _closeTimer = new DispatcherTimer { Interval = (TimeSpan)FindResource("HoverCloseDelay") };
        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer.Stop();
            Collapse();
        };

        _screen.LayoutChanged += (_, _) => PositionAtTopCentre();

        // Scaling changed (or the window moved to a monitor with another DPI):
        // WPF resizes the window first, so re-centre after that has happened.
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(PositionAtTopCentre);

        _hotkey.Pressed += (_, _) => TogglePanel();

        theme.ThemeChanged += (_, _) => ApplyThemeToShell();

        // The user clicked another window (or Alt+Tabbed away): close, and
        // leave focus where they put it.
        Deactivated += (_, _) =>
        {
            if (IsExpanded)
            {
                _closedByDeactivationAt = _time.GetTimestamp();
            }

            Collapse();
        };
    }

    /// <summary>
    /// The logical state. It changes immediately; the animation may still be
    /// catching up visually (see PillWindow.Animation.cs).
    /// </summary>
    private bool IsExpanded { get; set; }

    /// <summary>
    /// True only when Windows really gives us keyboard focus. We ask Windows
    /// instead of using WPF's IsActive: on a WS_EX_NOACTIVATE window, WPF sets
    /// IsActive as soon as it is clicked, even though focus never moved.
    /// </summary>
    private bool HasFocus => NativeMethods.GetForegroundWindow() == Handle;

    private nint Handle => new WindowInteropHelper(this).Handle;

    /// <summary>
    /// Runs once the native window (HWND) exists but before it is shown:
    /// the right moment to change its extended styles and place it.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowStyles.ApplyOverlayStyles(Handle);
        PositionAtTopCentre();
        RegisterHotkey();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        // While asking "Stop the current session and start a new one?":
        // Enter = Start new, Esc = Keep current, and the panel stays open.
        if (IsExpanded && _timer.IsAskingToReplace && e.Key is Key.Enter or Key.Escape)
        {
            e.Handled = true;
            if (e.Key == Key.Enter)
            {
                _timer.ConfirmReplaceCommand.Execute(null);
            }
            else
            {
                _timer.KeepCurrentCommand.Execute(null);
            }

            return;
        }

        // Esc: cancel a rename first (the rename box handles that itself),
        // otherwise close the panel (DESIGN.md §5).
        if (e.Key == Key.Escape && IsExpanded && !_tasks.IsRenaming)
        {
            e.Handled = true;
            CloseAndReturnFocus();
            return;
        }

        // Alt+F4 would close this window and with it the whole app. Only Quit
        // exits (PRD F5.4), so treat it like Esc. With Alt held, WPF reports
        // the key as Key.System and puts the real key in SystemKey.
        if (e.Key == Key.System && e.SystemKey == Key.F4)
        {
            e.Handled = true;
            if (IsExpanded)
            {
                CloseAndReturnFocus();
            }
        }
    }

    // --- Hover ---------------------------------------------------------------

    private void OnHoverZoneMouseEnter(object sender, MouseEventArgs e)
    {
        // Mouse came back before the close delay ran out: stay open.
        _closeTimer.Stop();

        if (!IsExpanded)
        {
            _openTimer.Start();
        }
    }

    private void OnHoverZoneMouseLeave(object sender, MouseEventArgs e)
    {
        // Mouse only passed through: cancel the pending open.
        _openTimer.Stop();

        // A focused panel stays open, so a half-typed task isn't lost (PRD F2.6).
        if (IsExpanded && !HasFocus)
        {
            _closeTimer.Start();
        }
    }

    // --- Click ---------------------------------------------------------------

    /// <summary>Clicking the collapsed pill = same as the hotkey (DESIGN.md §5).</summary>
    private void OnPillMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // The shell is also the panel once expanded; only a click on the
        // collapsed pill should open-with-focus.
        if (IsExpanded)
        {
            return;
        }

        e.Handled = true;
        OpenWithFocus();
    }

    /// <summary>
    /// Clicking anywhere inside a hover-opened panel: take focus first, then let
    /// the click continue to whatever was clicked (e.g. the text box).
    /// Hooked on the Shell (which paints the background), because clicks on empty,
    /// unpainted parts of the content layer are never delivered to that layer.
    /// </summary>
    private void OnPanelPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsExpanded && !HasFocus)
        {
            TakeFocus();
        }

        // A click anywhere outside an open rename box saves it (DESIGN.md §2).
        Views.RenameBox.CommitIfClickedOutside();
    }

    // --- Hotkey --------------------------------------------------------------

    /// <summary>
    /// Registers the hotkey from settings.json (default Win+Alt+L, DESIGN.md §5).
    /// If another app owns it, tell the user once; hover still works, so the
    /// app keeps running.
    /// </summary>
    private void RegisterHotkey()
    {
        var text = _settings.Current.Hotkey;
        if (HotkeyText.Parse(text) is not { } gesture)
        {
            // A hand-edited settings.json with a hotkey we can't read: use the default.
            _log.LogWarning("Hotkey {Hotkey} in settings.json can't be read; using {Default}", text, AppSettings.DefaultHotkey);
            gesture = HotkeyText.Parse(AppSettings.DefaultHotkey)!;
        }

        if (_hotkey.TryRegister(gesture))
        {
            return;
        }

        var shown = HotkeyText.ToShown(gesture);
        _log.LogWarning("Hotkey {Hotkey} is already used by another app", shown);

        // Shown the same way as in Settings ("Win + Alt + L"); WPF's own display
        // string would reorder it ("Alt+Windows+L").
        var message = string.Format(
            CultureInfo.CurrentCulture,
            (string)FindResource("HotkeyTakenMessage"),
            shown);

        // Show it after start-up finishes, so the pill appears first.
        Dispatcher.BeginInvoke(() =>
            MessageBox.Show(message, (string)FindResource("AppName"), MessageBoxButton.OK, MessageBoxImage.Warning));
    }

    /// <summary>Tray icon left-click: like the hotkey, minus the click that just closed the panel.</summary>
    public void TogglePanelFromTray()
    {
        if (_time.GetElapsedTime(_closedByDeactivationAt) < TrayClickGrace)
        {
            return;
        }

        TogglePanel();
    }

    /// <summary>Tray "Open", or a second launch of Ledge (PRD F5.2): open with focus; no-op if already there.</summary>
    public void OpenPanel()
    {
        if (IsExpanded && HasFocus)
        {
            return;
        }

        OpenWithFocus();
    }

    /// <summary>
    /// Hotkey: focused and open → close. Anything else (closed, or hover-opened
    /// without focus) → open with the cursor in quick-add.
    /// </summary>
    private void TogglePanel()
    {
        if (IsExpanded && HasFocus)
        {
            CloseAndReturnFocus();
        }
        else
        {
            OpenWithFocus();
        }
    }

    // --- Open / close ----------------------------------------------------------

    private void OpenWithFocus()
    {
        Expand();
        TakeFocus();

        // The panel was only just made visible, so WPF hasn't laid it out yet and
        // the text box can't take focus. Run FocusInput after the layout pass.
        // https://learn.microsoft.com/dotnet/api/system.windows.threading.dispatcherpriority
        Dispatcher.BeginInvoke(QuickAdd.FocusInput, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Remembers the app the user was in, then lets this window be activated.
    /// Allowed by Windows because the user just interacted with us (hotkey or click).
    /// </summary>
    private void TakeFocus()
    {
        _openTimer.Stop();
        _closeTimer.Stop();

        // Re-opened while a hand-back was still waiting: cancel it, but keep
        // remembering that app so the next close returns there.
        if (_pendingFocusReturn != 0)
        {
            _returnFocusTimer.Stop();
            _previousForeground = _pendingFocusReturn;
            _pendingFocusReturn = 0;
        }

        // Remember the app the user is in, unless that's already us
        // (Capture returns 0 for our own window, so we keep the earlier value).
        var foreground = ForegroundWindow.Capture(Handle);
        if (foreground != 0)
        {
            _previousForeground = foreground;
        }

        WindowStyles.SetNoActivate(Handle, noActivate: false);
        Activate();
    }

    /// <summary>
    /// Esc or hotkey: close now, and put focus back where it was as soon as
    /// the modifier keys are released.
    /// </summary>
    /// <remarks>
    /// Why wait: the hotkey fires when L goes down, while Win+Alt are still held.
    /// If we handed focus back immediately, the other app would receive the
    /// "Alt released" key-up, and in most apps a lone Alt tap activates the
    /// menu bar — swallowing the user's next keystroke. Waiting means the
    /// key-up lands in Ledge, where it does nothing.
    /// </remarks>
    private void CloseAndReturnFocus()
    {
        _pendingFocusReturn = _previousForeground;
        _returnFocusPolls = 0;
        Collapse();
        TryReturnFocus();
    }

    private void TryReturnFocus()
    {
        var maxPolls = (int)(ReturnFocusMaxWait / ReturnFocusPollInterval);
        if (ModifierKeysState.AnyDown() && _returnFocusPolls < maxPolls)
        {
            _returnFocusPolls++;
            _returnFocusTimer.Start();
            return;
        }

        _returnFocusTimer.Stop();
        var target = _pendingFocusReturn;
        _pendingFocusReturn = 0;

        // Only hand back if we still have focus: if the user clicked somewhere
        // else meanwhile, leave focus where they put it. (While we are the
        // foreground window, Windows allows this hand-back.)
        if (HasFocus)
        {
            ForegroundWindow.Restore(target);
        }
    }

    private void Expand()
    {
        _openTimer.Stop();
        _closeTimer.Stop();

        if (IsExpanded)
        {
            return;
        }

        IsExpanded = true;
        _tasks.Refresh(); // before measuring, so the panel opens at the right height
        AnimateExpand();
    }

    /// <summary>
    /// Hides the panel and makes the window non-activatable again, so hovering
    /// and clicking the pill never steal focus while collapsed.
    /// </summary>
    private void Collapse()
    {
        _openTimer.Stop();
        _closeTimer.Stop();

        if (IsExpanded)
        {
            IsExpanded = false;
            AnimateCollapse();
        }

        _previousForeground = 0;
        WindowStyles.SetNoActivate(Handle, noActivate: true);

        // Undo only works while the panel is open (PRD F3.4); notices are seen once.
        _tasks.OnPanelClosed();
    }

    // --- Placement -------------------------------------------------------------

    /// <summary>
    /// Centres the window horizontally on the primary monitor's work area and
    /// puts it at the top. Everything is in physical pixels (see WindowPlacement).
    /// </summary>
    private void PositionAtTopCentre()
    {
        var workArea = ScreenService.GetPrimaryWorkArea();
        var (width, _) = WindowPlacement.GetSize(Handle);

        var x = workArea.X + ((workArea.Width - width) / 2);
        var y = workArea.Y;

        WindowPlacement.MoveTo(Handle, x, y);
    }
}
