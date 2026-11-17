using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ledge.Core.Abstractions;
using Ledge.Core.Models;
using Ledge.Core.Services;

namespace Ledge.ViewModels;

/// <summary>
/// The timer row in the panel (PRD F4, DESIGN.md §2 "Timer row"): preset
/// chips, start / pause / resume / stop, and the "replace the running timer?"
/// question. Also tells the task list which task is linked (for its marker).
/// </summary>
/// <remarks>
/// The Core timer raises its events on a background thread, but bound
/// properties must change on the UI thread. So every timer event is posted to
/// the UI thread's SynchronizationContext, given to the constructor.
/// https://learn.microsoft.com/dotnet/api/system.threading.synchronizationcontext
/// </remarks>
public sealed partial class TimerViewModel : ObservableObject
{
    /// <summary>PRD F4: presets 15, 25 and 50 minutes; 25 is the default.</summary>
    public static readonly IReadOnlyList<int> PresetMinutes = [15, 25, 50];

    public const int DefaultPresetMinutes = 25;

    private readonly IFocusTimer _timer;
    private readonly SynchronizationContext? _uiThread;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle), nameof(IsRunning), nameof(IsPaused))]
    private FocusTimerState _state;

    /// <summary>"18:42"; empty when idle.</summary>
    [ObservableProperty]
    private string _remainingText = string.Empty;

    [ObservableProperty]
    private int _selectedMinutes = DefaultPresetMinutes;

    /// <summary>The task the running (or paused) session is for; null if none.</summary>
    [ObservableProperty]
    private Guid? _linkedTaskId;

    /// <summary>The row shows "Stop the current session and start a new one?".</summary>
    [ObservableProperty]
    private bool _isAskingToReplace;

    /// <summary>The task a waiting replacement would be linked to.</summary>
    private Guid? _pendingTaskId;

    /// <param name="timer">The Core timer.</param>
    /// <param name="uiThread">
    /// Where to run updates: the UI thread's context in the app; null in tests,
    /// to run them straight away. (Passed in, not read from
    /// SynchronizationContext.Current, because test runners install their own.)
    /// </param>
    public TimerViewModel(IFocusTimer timer, SynchronizationContext? uiThread)
    {
        _timer = timer;
        _uiThread = uiThread;

        _timer.Tick += (_, _) => OnUiThread(UpdateFromTimer);
        _timer.StateChanged += (_, _) => OnUiThread(UpdateFromTimer);
        UpdateFromTimer();
    }

    public bool IsIdle => State == FocusTimerState.Idle;

    public bool IsRunning => State == FocusTimerState.Running;

    public bool IsPaused => State == FocusTimerState.Paused;

    /// <summary>
    /// Start with the selected preset, linked to <paramref name="taskId"/> if
    /// given (Ctrl+Enter on a task). If a session is already going, ask first
    /// (PRD F4.7) instead of replacing it.
    /// </summary>
    public void RequestStart(Guid? taskId)
    {
        if (State == FocusTimerState.Idle)
        {
            _timer.Start(TimeSpan.FromMinutes(SelectedMinutes), taskId);
            return;
        }

        _pendingTaskId = taskId;
        IsAskingToReplace = true;
    }

    /// <summary>
    /// "mm:ss", rounding UP (so 0.4 s left shows 00:01, not 00:00), but
    /// ignoring the few milliseconds a tick can arrive early
    /// (FocusTimer.ClockTolerance): 24:59.0005 left shows 24:59, not 25:00.
    /// </summary>
    public static string Format(TimeSpan remaining)
    {
        var totalSeconds = Math.Max(0, (int)Math.Ceiling((remaining - FocusTimer.ClockTolerance).TotalSeconds));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{totalSeconds / 60:00}:{totalSeconds % 60:00}");
    }

    /// <summary>Click on a preset chip (only shown while idle).</summary>
    [RelayCommand]
    private void SelectPreset(int minutes)
    {
        if (PresetMinutes.Contains(minutes))
        {
            SelectedMinutes = minutes;
        }
    }

    /// <summary>▶ while idle: an unlinked session (PRD F4.1).</summary>
    [RelayCommand]
    private void Start() => RequestStart(taskId: null);

    [RelayCommand]
    private void Pause() => _timer.Pause();

    [RelayCommand]
    private void Resume() => _timer.Resume();

    /// <summary>■: ends the session; asks nothing, records nothing (PRD F4.3).</summary>
    [RelayCommand]
    private void Stop() => _timer.Stop();

    /// <summary>"Start new" (or Enter): replace the running session.</summary>
    [RelayCommand]
    private void ConfirmReplace()
    {
        if (!IsAskingToReplace)
        {
            return;
        }

        IsAskingToReplace = false;
        _timer.Start(TimeSpan.FromMinutes(SelectedMinutes), _pendingTaskId);
        _pendingTaskId = null;
    }

    /// <summary>"Keep current" (or Esc): forget the request.</summary>
    [RelayCommand]
    private void KeepCurrent()
    {
        IsAskingToReplace = false;
        _pendingTaskId = null;
    }

    private void UpdateFromTimer()
    {
        State = _timer.State;
        LinkedTaskId = _timer.Session?.TaskId;
        RemainingText = State == FocusTimerState.Idle ? string.Empty : Format(_timer.Remaining);

        // The session ended (stopped or finished) while we were asking: nothing left to replace.
        if (State == FocusTimerState.Idle && IsAskingToReplace)
        {
            KeepCurrent();
        }
    }

    private void OnUiThread(Action action)
    {
        if (_uiThread is null)
        {
            action();
            return;
        }

        _uiThread.Post(_ => action(), null);
    }
}
