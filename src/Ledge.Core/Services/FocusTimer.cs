using Ledge.Core.Abstractions;
using Ledge.Core.Models;

namespace Ledge.Core.Services;

/// <summary>
/// The focus timer (PRD F4, ADR-0004). It never counts down: the remaining
/// time is always computed from the session's timestamps and the clock, so it
/// can't drift and is right again the moment the PC wakes up.
/// </summary>
/// <remarks>
/// A TimeProvider timer fires once a second while running. Each tick does two
/// things: raises <see cref="Tick"/> so the UI can redraw, and checks whether
/// the time is up. Nothing ticks while idle or paused, so it costs no CPU then.
/// Ticks arrive on a thread-pool thread, so state changes are guarded by a lock.
/// </remarks>
public sealed class FocusTimer : IFocusTimer, IDisposable
{
    /// <summary>The display shows whole seconds (mm:ss).</summary>
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private readonly TimeProvider _time;
    private readonly ITimer _ticker;
    private readonly Lock _gate = new();
    private FocusSession? _session;

    public FocusTimer(TimeProvider time)
    {
        _time = time;
        _ticker = time.CreateTimer(_ => OnTick(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public event EventHandler? Tick;

    public event EventHandler? StateChanged;

    public event EventHandler<FocusFinishedEventArgs>? Finished;

    public FocusSession? Session
    {
        get
        {
            lock (_gate)
            {
                return _session;
            }
        }
    }

    public FocusTimerState State => Session switch
    {
        null => FocusTimerState.Idle,
        { IsPaused: true } => FocusTimerState.Paused,
        _ => FocusTimerState.Running,
    };

    public TimeSpan Remaining => Session?.RemainingAt(_time.GetUtcNow()) ?? TimeSpan.Zero;

    public void Start(TimeSpan duration, Guid? taskId = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);

        lock (_gate)
        {
            _session = new FocusSession { StartedAt = _time.GetUtcNow(), Duration = duration, TaskId = taskId };
            StartTicking();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Pause()
    {
        lock (_gate)
        {
            if (_session is not { IsPaused: false } running)
            {
                return;
            }

            _session = running with { PausedAt = _time.GetUtcNow() };
            StopTicking();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Resume()
    {
        lock (_gate)
        {
            if (_session is not { PausedAt: { } pausedAt } paused)
            {
                return;
            }

            // Add this pause to the total; the end time moves later by as much.
            _session = paused with
            {
                PausedTotal = paused.PausedTotal + (_time.GetUtcNow() - pausedAt),
                PausedAt = null,
            };
            StartTicking();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (_session is null)
            {
                return;
            }

            _session = null;
            StopTicking();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void CheckNow() => OnTick();

    public void Dispose() => _ticker.Dispose();

    /// <summary>
    /// Once a second (and on <see cref="CheckNow"/>): finish if the time is up,
    /// otherwise tell the UI to redraw. Finishing clears the session inside the
    /// lock, so even two checks racing each other can only finish it once.
    /// </summary>
    private void OnTick()
    {
        FocusFinishedEventArgs? finished = null;

        lock (_gate)
        {
            if (_session is not { IsPaused: false } running)
            {
                return;
            }

            var now = _time.GetUtcNow();
            if (running.RemainingAt(now) == TimeSpan.Zero)
            {
                _session = null;
                StopTicking();
                finished = new FocusFinishedEventArgs(running, now - running.EndsAt);
            }
        }

        if (finished is null)
        {
            Tick?.Invoke(this, EventArgs.Empty);
            return;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
        Finished?.Invoke(this, finished);
    }

    /// <summary>
    /// Lines the ticks up with the remaining time's whole seconds: with 24:59.6
    /// left, the first tick comes after 0.6 s (at 24:59.0), then every second.
    /// So the mm:ss display changes on the second and zero is hit exactly.
    /// Call inside the lock with the current session.
    /// </summary>
    private void StartTicking()
    {
        var remaining = _session!.RemainingAt(_time.GetUtcNow());
        var partSecond = TimeSpan.FromTicks(remaining.Ticks % TickInterval.Ticks);
        var firstTick = partSecond > TimeSpan.Zero ? partSecond : TickInterval;

        _ticker.Change(firstTick, TickInterval);
    }

    private void StopTicking() => _ticker.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
}
