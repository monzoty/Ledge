namespace Ledge.Core.Services;

/// <summary>
/// Raises <see cref="DayChanged"/> when the local calendar date changes, so
/// lists can be recomputed while the app keeps running (PRD F3: "the day
/// changes at local midnight, including while the app is running").
/// </summary>
/// <remarks>
/// One timer is set for the next local midnight; nothing ticks in between, so
/// it costs no CPU. A timer can't see everything, though: the PC may sleep past
/// midnight, or the user may change the clock. The app therefore also calls
/// <see cref="Check"/> on wake-up and on clock changes. Check is safe to call
/// any time; it only raises the event if the date really changed.
/// DayChanged is raised on a background thread when it comes from the timer.
/// </remarks>
public sealed class LocalDayWatcher : IDisposable
{
    /// <summary>
    /// Never wait less than this. Around daylight-saving changes the computed
    /// delay can be slightly off; a timer that fires early just re-checks and
    /// re-schedules, and this stops that from becoming a busy loop.
    /// </summary>
    private static readonly TimeSpan MinimumDelay = TimeSpan.FromSeconds(1);

    private readonly TimeProvider _time;
    private readonly ITimer _timer;
    private readonly Lock _gate = new();
    private DateOnly _today;

    public LocalDayWatcher(TimeProvider time)
    {
        _time = time;
        _today = TaskQueries.CurrentDate(time);
        _timer = time.CreateTimer(_ => Check(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        ScheduleNextMidnight();
    }

    public event EventHandler? DayChanged;

    public DateOnly Today
    {
        get
        {
            lock (_gate)
            {
                return _today;
            }
        }
    }

    /// <summary>
    /// Compares the date now with the last one seen; raises DayChanged once if
    /// it differs (even if several days passed), then re-arms the timer.
    /// </summary>
    public void Check()
    {
        bool changed;
        lock (_gate)
        {
            var now = TaskQueries.CurrentDate(_time);
            changed = now != _today;
            _today = now;
        }

        ScheduleNextMidnight();

        if (changed)
        {
            DayChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose() => _timer.Dispose();

    private void ScheduleNextMidnight()
    {
        var now = _time.GetLocalNow();
        var nextMidnight = now.Date.AddDays(1);
        var delay = nextMidnight - now.DateTime;

        _timer.Change(delay < MinimumDelay ? MinimumDelay : delay, Timeout.InfiniteTimeSpan);
    }
}
