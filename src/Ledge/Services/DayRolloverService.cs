using System.Windows.Threading;
using Ledge.Core.Abstractions;
using Ledge.Core.Services;
using Ledge.ViewModels;
using Microsoft.Win32;

namespace Ledge.Services;

/// <summary>
/// Refreshes the task lists when the day changes while Ledge is running
/// (ARCHITECTURE.md §7 "Day rollover"): at midnight, after waking from sleep,
/// and when the user changes the clock or time zone.
/// </summary>
/// <remarks>
/// LocalDayWatcher (Core) decides WHETHER the day changed; this class only
/// feeds it Windows events and brings the result back to the UI thread.
/// </remarks>
public sealed class DayRolloverService : IDisposable
{
    private readonly LocalDayWatcher _watcher;
    private readonly TaskListViewModel _tasks;
    private readonly IFocusTimer _focusTimer;
    private readonly Dispatcher _dispatcher;

    public DayRolloverService(LocalDayWatcher watcher, TaskListViewModel tasks, IFocusTimer focusTimer)
    {
        _watcher = watcher;
        _tasks = tasks;
        _focusTimer = focusTimer;

        // Created on the UI thread (at start-up), so this is the UI dispatcher.
        _dispatcher = Dispatcher.CurrentDispatcher;

        _watcher.DayChanged += OnDayChanged;

        // SystemEvents are raised on a special background thread, and MUST be
        // unsubscribed (see Dispose): they're static, so a forgotten handler
        // keeps this object alive for the whole process.
        // https://learn.microsoft.com/dotnet/api/microsoft.win32.systemevents
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.TimeChanged += OnTimeChanged;
    }

    public void Dispose()
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.TimeChanged -= OnTimeChanged;
        _watcher.DayChanged -= OnDayChanged;
    }

    /// <summary>
    /// The PC woke up: midnight may have passed while it slept, and a focus
    /// session may have run out (PRD F4.5). Check both against the clock now
    /// rather than waiting for the next timer tick.
    /// </summary>
    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            _watcher.Check();
            _focusTimer.CheckNow();
        }
    }

    /// <summary>The clock or time zone was changed.</summary>
    private void OnTimeChanged(object? sender, EventArgs e) => _watcher.Check();

    /// <summary>
    /// Raised on a background thread; ViewModels must only be touched on the UI
    /// thread, so hand the refresh over to it.
    /// </summary>
    private void OnDayChanged(object? sender, EventArgs e) => _dispatcher.BeginInvoke(_tasks.Refresh);
}
