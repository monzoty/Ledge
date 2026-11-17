using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ledge.Core.Models;
using Ledge.Core.Services;
using Ledge.Services;

namespace Ledge.ViewModels;

/// <summary>
/// The Settings window (PRD F6, DESIGN.md §5 "Settings window"). Every change
/// applies and saves at once; there's no Save button.
/// </summary>
/// <remarks>
/// <c>partial void OnThemeChanged(…)</c> and friends are hooks that
/// CommunityToolkit.Mvvm calls after the generated property changes; that's
/// where a change is applied and saved.
/// https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/generators/observableproperty
/// </remarks>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly IGlobalHotkey _hotkey;
    private readonly IStartupRegistration _startup;

    /// <summary>True only inside the constructor: loading values isn't a change to save.</summary>
    private readonly bool _isInitializing;

    /// <summary>The current shortcut as shown, e.g. "Win + Alt + L".</summary>
    [ObservableProperty]
    private string _shortcutText = string.Empty;

    /// <summary>The box is waiting for a key combination ("Press a shortcut…").</summary>
    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private ShortcutProblem _shortcutProblem;

    /// <summary>The shortcut that was taken, for "{hotkey} is already used by another app."</summary>
    [ObservableProperty]
    private string _takenShortcutText = string.Empty;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private ThemeMode _theme;

    public SettingsViewModel(SettingsService settings, IGlobalHotkey hotkey, IStartupRegistration startup)
    {
        _settings = settings;
        _hotkey = hotkey;
        _startup = startup;

        // Loading the window must not count as a change (which would save and
        // touch the registry), so the On…Changed hooks skip while this is set.
        _isInitializing = true;
        ShortcutText = ShownShortcut();
        StartWithWindows = settings.Current.StartWithWindows;
        Theme = settings.Current.Theme;
        _isInitializing = false;
    }

    /// <summary>
    /// A key went down while recording (the window passes it in, already
    /// translated: Alt combinations arrive as their real key, not "System").
    /// Bare modifier keys are ignored, so you can press Win, then Alt, then L.
    /// </summary>
    public void CaptureShortcut(Key key, ModifierKeys modifiers)
    {
        if (!IsRecording || HotkeyText.IsModifierKey(key))
        {
            return;
        }

        if (key == Key.Escape && modifiers == ModifierKeys.None)
        {
            CancelRecording();
            return;
        }

        if (!HotkeyText.IsAllowed(modifiers, key) || TryCreateGesture(key, modifiers) is not { } gesture)
        {
            // Stay in recording mode so the next try is just another key press.
            ShortcutProblem = ShortcutProblem.Invalid;
            return;
        }

        if (!_hotkey.TryChange(gesture))
        {
            TakenShortcutText = HotkeyText.ToShown(gesture);
            ShortcutProblem = ShortcutProblem.Taken;
            return;
        }

        ShortcutProblem = ShortcutProblem.None;
        IsRecording = false;
        ShortcutText = HotkeyText.ToShown(gesture);
        _ = _settings.UpdateAsync(s => s with { Hotkey = HotkeyText.ToStored(gesture) });
    }

    /// <summary>
    /// Click on the shortcut box. The live hotkey is switched off meanwhile, so
    /// pressing it reaches this window instead of toggling the panel.
    /// </summary>
    [RelayCommand]
    private void StartRecording()
    {
        if (IsRecording)
        {
            return;
        }

        ShortcutProblem = ShortcutProblem.None;
        IsRecording = true;
        _hotkey.Suspend();
    }

    /// <summary>Esc, clicking elsewhere, or closing the window: keep the old shortcut.</summary>
    [RelayCommand]
    private void CancelRecording()
    {
        if (!IsRecording)
        {
            return;
        }

        IsRecording = false;
        ShortcutProblem = ShortcutProblem.None;
        _hotkey.Resume();
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_isInitializing)
        {
            return;
        }

        _startup.SetEnabled(value);
        _ = _settings.UpdateAsync(s => s with { StartWithWindows = value });
    }

    /// <summary>Saved now; the light colours themselves arrive in M4 step 4d.</summary>
    partial void OnThemeChanged(ThemeMode value)
    {
        if (_isInitializing)
        {
            return;
        }

        _ = _settings.UpdateAsync(s => s with { Theme = value });
    }

    /// <summary>
    /// What to show in the box: the registered shortcut, or, if none could be
    /// registered at start-up (taken), what settings.json asks for.
    /// </summary>
    private string ShownShortcut()
    {
        var gesture = _hotkey.Current ?? HotkeyText.Parse(_settings.Current.Hotkey);
        return gesture is null ? _settings.Current.Hotkey : HotkeyText.ToShown(gesture);
    }

    /// <summary>KeyGesture refuses a few odd combinations itself; treat those as invalid.</summary>
    private static KeyGesture? TryCreateGesture(Key key, ModifierKeys modifiers)
    {
        try
        {
            return new KeyGesture(key, modifiers);
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
