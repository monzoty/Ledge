using Ledge.Core.Models;

namespace Ledge.Core.Abstractions;

/// <summary>
/// The one focus timer (PRD F4). An interface so ViewModels can be tested
/// with a fake. Events may be raised on a background thread.
/// </summary>
public interface IFocusTimer
{
    /// <summary>Once a second while running: time to redraw (remaining time changed).</summary>
    event EventHandler? Tick;

    /// <summary>Started, paused, resumed, stopped or finished.</summary>
    event EventHandler? StateChanged;

    /// <summary>The session reached zero. Raised exactly once per session.</summary>
    event EventHandler<FocusFinishedEventArgs>? Finished;

    FocusTimerState State { get; }

    /// <summary>The current session; null when <see cref="FocusTimerState.Idle"/>.</summary>
    FocusSession? Session { get; }

    /// <summary>Time left now; zero when idle.</summary>
    TimeSpan Remaining { get; }

    /// <summary>
    /// Starts a session, replacing any current one without finishing it
    /// (PRD F4.7; asking for confirmation is the UI's job).
    /// </summary>
    void Start(TimeSpan duration, Guid? taskId = null);

    void Pause();

    void Resume();

    /// <summary>Ends the session without a "finished" event (PRD F4.3: records nothing).</summary>
    void Stop();

    /// <summary>
    /// Re-checks against the clock right now, e.g. after the PC wakes up. If
    /// the time ran out meanwhile, raises <see cref="Finished"/> (PRD F4.5).
    /// </summary>
    void CheckNow();
}
