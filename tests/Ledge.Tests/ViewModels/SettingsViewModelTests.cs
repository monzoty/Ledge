using System.Windows.Input;
using Ledge.Core.Abstractions;
using Ledge.Core.Models;
using Ledge.Core.Services;
using Ledge.Services;
using Ledge.ViewModels;

namespace Ledge.Tests.ViewModels;

public sealed class SettingsViewModelTests
{
    private static readonly KeyGesture WinAltL = new(Key.L, ModifierKeys.Windows | ModifierKeys.Alt);

    private readonly FakeSettingsStore _store = new();
    private readonly FakeHotkey _hotkey = new() { Current = WinAltL };
    private readonly FakeStartup _startup = new();
    private readonly SettingsService _settings;
    private readonly SettingsViewModel _viewModel;

    public SettingsViewModelTests()
    {
        _settings = new SettingsService(_store, new AppSettings());
        _viewModel = new SettingsViewModel(_settings, _hotkey, _startup);
    }

    private void Record() => _viewModel.StartRecordingCommand.Execute(null);

    // --- Opening ------------------------------------------------------------------

    [Fact]
    public void Opening_ShowsCurrentValues_WithoutSavingAnything()
    {
        Assert.Equal("Win + Alt + L", _viewModel.ShortcutText);
        Assert.False(_viewModel.StartWithWindows);
        Assert.Equal(ThemeMode.System, _viewModel.Theme);
        Assert.False(_viewModel.IsRecording);

        Assert.Empty(_store.Saved);
        Assert.Empty(_startup.Calls);
    }

    [Fact]
    public void Opening_WithSavedValues_ShowsThem()
    {
        var settings = new SettingsService(_store, new AppSettings { StartWithWindows = true, Theme = ThemeMode.Dark });

        var viewModel = new SettingsViewModel(settings, _hotkey, _startup);

        Assert.True(viewModel.StartWithWindows);
        Assert.Equal(ThemeMode.Dark, viewModel.Theme);
        Assert.Empty(_startup.Calls);
    }

    [Fact]
    public void Opening_WhenHotkeyWasTakenAtStartUp_ShowsTheSavedShortcut()
    {
        _hotkey.Current = null;

        var viewModel = new SettingsViewModel(_settings, _hotkey, _startup);

        Assert.Equal("Win + Alt + L", viewModel.ShortcutText);
    }

    // --- Recording a shortcut -------------------------------------------------------

    [Fact]
    public void StartRecording_SuspendsTheLiveHotkey()
    {
        Record();

        Assert.True(_viewModel.IsRecording);
        Assert.True(_hotkey.IsSuspended);
    }

    [Fact]
    public void ValidShortcut_IsApplied_ShownAndSaved()
    {
        Record();

        _viewModel.CaptureShortcut(Key.Space, ModifierKeys.Control | ModifierKeys.Alt);

        Assert.False(_viewModel.IsRecording);
        Assert.Equal(ShortcutProblem.None, _viewModel.ShortcutProblem);
        Assert.Equal("Ctrl + Alt + Space", _viewModel.ShortcutText);
        Assert.Equal(new KeyGesture(Key.Space, ModifierKeys.Control | ModifierKeys.Alt), _hotkey.Current, GestureComparer.Instance);
        Assert.Equal("Ctrl+Alt+Space", _settings.Current.Hotkey);
        Assert.Equal("Ctrl+Alt+Space", Assert.Single(_store.Saved).Hotkey);
    }

    [Fact]
    public void ModifierKeysAlone_AreIgnored_WhileWaitingForTheRealKey()
    {
        Record();

        _viewModel.CaptureShortcut(Key.LWin, ModifierKeys.Windows);
        _viewModel.CaptureShortcut(Key.LeftAlt, ModifierKeys.Windows | ModifierKeys.Alt);

        Assert.True(_viewModel.IsRecording);
        Assert.Equal(ShortcutProblem.None, _viewModel.ShortcutProblem);
    }

    [Theory]
    [InlineData(Key.L, ModifierKeys.Control)]                       // Ctrl+L: apps use it
    [InlineData(Key.N, ModifierKeys.Control | ModifierKeys.Shift)]  // Ctrl+Shift+N
    [InlineData(Key.F, ModifierKeys.Alt)]                           // Alt+F: menus
    [InlineData(Key.Q, ModifierKeys.None)]
    public void DisallowedShortcut_ShowsInvalid_AndKeepsRecording(Key key, ModifierKeys modifiers)
    {
        Record();

        _viewModel.CaptureShortcut(key, modifiers);

        Assert.Equal(ShortcutProblem.Invalid, _viewModel.ShortcutProblem);
        Assert.True(_viewModel.IsRecording);
        Assert.Empty(_hotkey.ChangeAttempts);
        Assert.Empty(_store.Saved);
    }

    [Fact]
    public void TakenShortcut_ShowsTaken_AndKeepsOldOne()
    {
        _hotkey.Taken = true;
        Record();

        _viewModel.CaptureShortcut(Key.K, ModifierKeys.Windows | ModifierKeys.Shift);

        Assert.Equal(ShortcutProblem.Taken, _viewModel.ShortcutProblem);
        Assert.Equal("Win + Shift + K", _viewModel.TakenShortcutText);
        Assert.Equal("Win + Alt + L", _viewModel.ShortcutText);
        Assert.True(_viewModel.IsRecording);
        Assert.Empty(_store.Saved);
    }

    [Fact]
    public void Escape_CancelsRecording_AndResumesOldHotkey()
    {
        Record();

        _viewModel.CaptureShortcut(Key.Escape, ModifierKeys.None);

        Assert.False(_viewModel.IsRecording);
        Assert.False(_hotkey.IsSuspended);
        Assert.Equal("Win + Alt + L", _viewModel.ShortcutText);
    }

    [Fact]
    public void CancelRecording_ClearsProblem()
    {
        Record();
        _viewModel.CaptureShortcut(Key.L, ModifierKeys.Control);

        _viewModel.CancelRecordingCommand.Execute(null);

        Assert.Equal(ShortcutProblem.None, _viewModel.ShortcutProblem);
        Assert.False(_hotkey.IsSuspended);
    }

    [Fact]
    public void KeysWhileNotRecording_AreIgnored()
    {
        _viewModel.CaptureShortcut(Key.Space, ModifierKeys.Control | ModifierKeys.Alt);

        Assert.Empty(_hotkey.ChangeAttempts);
        Assert.Equal("Win + Alt + L", _viewModel.ShortcutText);
    }

    [Fact]
    public void DigitKeys_AreShownAsDigits()
    {
        Record();

        _viewModel.CaptureShortcut(Key.D1, ModifierKeys.Windows | ModifierKeys.Alt);

        Assert.Equal("Win + Alt + 1", _viewModel.ShortcutText);
        Assert.Equal("Win+Alt+1", _settings.Current.Hotkey);

        // Readable again at the next start, as the same key.
        var reread = HotkeyText.Parse(_settings.Current.Hotkey);
        Assert.Equal(Key.D1, reread?.Key);
        Assert.Equal(ModifierKeys.Windows | ModifierKeys.Alt, reread?.Modifiers);
    }

    // --- Other settings -----------------------------------------------------------

    [Fact]
    public void StartWithWindows_UpdatesRegistryAndSaves()
    {
        _viewModel.StartWithWindows = true;

        Assert.Equal([true], _startup.Calls);
        Assert.True(_settings.Current.StartWithWindows);

        _viewModel.StartWithWindows = false;

        Assert.Equal([true, false], _startup.Calls);
        Assert.False(_store.Saved[^1].StartWithWindows);
    }

    [Fact]
    public void Theme_IsSaved()
    {
        _viewModel.Theme = ThemeMode.Light;

        Assert.Equal(ThemeMode.Light, _settings.Current.Theme);
        Assert.Equal(ThemeMode.Light, Assert.Single(_store.Saved).Theme);
    }

    // --- Fakes ----------------------------------------------------------------------

    private sealed class FakeHotkey : IGlobalHotkey
    {
        public KeyGesture? Current { get; set; }

        public bool IsSuspended { get; private set; }

        public bool Taken { get; set; }

        public List<KeyGesture> ChangeAttempts { get; } = [];

        public bool TryChange(KeyGesture gesture)
        {
            ChangeAttempts.Add(gesture);
            if (Taken)
            {
                return false;
            }

            Current = gesture;
            IsSuspended = false;
            return true;
        }

        public void Suspend() => IsSuspended = true;

        public void Resume() => IsSuspended = false;
    }

    private sealed class FakeStartup : IStartupRegistration
    {
        public List<bool> Calls { get; } = [];

        public bool IsEnabled => Calls.Count > 0 && Calls[^1];

        public void SetEnabled(bool enabled) => Calls.Add(enabled);
    }

    private sealed class FakeSettingsStore : ISettingsStore
    {
        public List<AppSettings> Saved { get; } = [];

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AppSettings());

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            Saved.Add(settings);
            return Task.CompletedTask;
        }
    }

    /// <summary>KeyGesture has no value equality; compare key and modifiers.</summary>
    private sealed class GestureComparer : IEqualityComparer<KeyGesture?>
    {
        public static readonly GestureComparer Instance = new();

        public bool Equals(KeyGesture? x, KeyGesture? y) =>
            x?.Key == y?.Key && x?.Modifiers == y?.Modifiers;

        public int GetHashCode(KeyGesture? obj) => HashCode.Combine(obj?.Key, obj?.Modifiers);
    }
}
