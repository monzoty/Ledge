using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using Ledge.Core.Abstractions;
using Ledge.Core.Models;
using Ledge.ViewModels;
using Microsoft.Extensions.Logging;

namespace Ledge.Services;

/// <summary>
/// What happens when a focus session ends (PRD F4.4–F4.6, DESIGN.md §2):
/// a tray notification, the "Mark task done" line in the panel for a linked
/// task, and <see cref="Completed"/> so the window can pulse the pill.
/// </summary>
public sealed class FocusCompletionService : IDisposable
{
    private readonly IFocusTimer _timer;
    private readonly TaskListViewModel _tasks;
    private readonly TrayService _tray;
    private readonly TimeProvider _time;
    private readonly ILogger<FocusCompletionService> _log;
    private readonly Dispatcher _dispatcher;

    public FocusCompletionService(
        IFocusTimer timer,
        TaskListViewModel tasks,
        TrayService tray,
        TimeProvider time,
        ILogger<FocusCompletionService> log)
    {
        _timer = timer;
        _tasks = tasks;
        _tray = tray;
        _time = time;
        _log = log;

        // Created on the UI thread at start-up.
        _dispatcher = Dispatcher.CurrentDispatcher;
        _timer.Finished += OnFinished;
    }

    /// <summary>A session ended; raised on the UI thread after the notification (for the pill pulse).</summary>
    public event EventHandler? Completed;

    public void Dispose() => _timer.Finished -= OnFinished;

    /// <summary>The timer reports on a background thread; everything here touches UI, so hop over.</summary>
    private void OnFinished(object? sender, FocusFinishedEventArgs e) => _dispatcher.BeginInvoke(() => Complete(e));

    private void Complete(FocusFinishedEventArgs e)
    {
        // Counts and timings only, never the task title (ARCHITECTURE.md §8).
        _log.LogInformation(
            "Focus session finished ({Minutes} min, linked: {Linked}, noticed {LateSeconds:0} s late)",
            e.Session.Duration.TotalMinutes,
            e.Session.TaskId is not null,
            e.LateBy.TotalSeconds);

        var taskTitle = _tasks.OnFocusFinished(e.Session.TaskId);
        var message = FocusDoneMessage.Create(e, taskTitle, _time.GetLocalNow(), Texts(), CultureInfo.CurrentCulture);
        _tray.ShowNotification(message.Title, message.Body);

        Completed?.Invoke(this, EventArgs.Empty);
    }

    private static FocusDoneTexts Texts()
    {
        var app = Application.Current;
        return new FocusDoneTexts(
            (string)app.FindResource("FocusDoneTitle"),
            (string)app.FindResource("FocusDoneLinked"),
            (string)app.FindResource("FocusDoneUnlinked"),
            (string)app.FindResource("FocusEndedTitle"),
            (string)app.FindResource("FocusEndedBody"));
    }
}
