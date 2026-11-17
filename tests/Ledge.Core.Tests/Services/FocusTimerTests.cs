using Ledge.Core.Models;
using Ledge.Core.Services;
using Microsoft.Extensions.Time.Testing;

namespace Ledge.Core.Tests.Services;

public sealed class FocusTimerTests : IDisposable
{
    private static readonly TimeSpan TwentyFive = TimeSpan.FromMinutes(25);

    private readonly FakeTimeProvider _time = TestClock.Create();
    private readonly FocusTimer _timer;
    private readonly List<FocusFinishedEventArgs> _finished = [];
    private int _ticks;
    private int _stateChanges;

    public FocusTimerTests()
    {
        _timer = new FocusTimer(_time);
        _timer.Finished += (_, e) => _finished.Add(e);
        _timer.Tick += (_, _) => _ticks++;
        _timer.StateChanged += (_, _) => _stateChanges++;
    }

    public void Dispose() => _timer.Dispose();

    private static TimeSpan Minutes(double m) => TimeSpan.FromMinutes(m);

    // --- Idle ---------------------------------------------------------------------

    [Fact]
    public void StartsIdle()
    {
        Assert.Equal(FocusTimerState.Idle, _timer.State);
        Assert.Null(_timer.Session);
        Assert.Equal(TimeSpan.Zero, _timer.Remaining);
    }

    [Fact]
    public void PauseResumeStop_WhenIdle_DoNothing()
    {
        _timer.Pause();
        _timer.Resume();
        _timer.Stop();
        _timer.CheckNow();

        Assert.Equal(0, _stateChanges);
        Assert.Empty(_finished);
    }

    [Fact]
    public void Start_RejectsZeroOrNegativeDuration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _timer.Start(TimeSpan.Zero));
    }

    // --- Running ------------------------------------------------------------------

    [Fact]
    public void Start_RunsWithFullDuration()
    {
        _timer.Start(TwentyFive);

        Assert.Equal(FocusTimerState.Running, _timer.State);
        Assert.Equal(TwentyFive, _timer.Remaining);
        Assert.Equal(1, _stateChanges);
    }

    [Fact]
    public void Start_CanLinkATask()
    {
        var taskId = Guid.NewGuid();

        _timer.Start(TwentyFive, taskId);

        Assert.Equal(taskId, _timer.Session!.TaskId);
    }

    [Fact]
    public void Remaining_FollowsTheClock()
    {
        _timer.Start(TwentyFive);

        _time.Advance(Minutes(10));

        Assert.Equal(Minutes(15), _timer.Remaining);
    }

    [Fact]
    public void Ticks_OncePerSecond_WhileRunning()
    {
        _timer.Start(TwentyFive);

        _time.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(10, _ticks);
    }

    // --- Finishing ----------------------------------------------------------------

    [Fact]
    public void Finishes_ExactlyAtZero_ExactlyOnce()
    {
        var taskId = Guid.NewGuid();
        _timer.Start(TwentyFive, taskId);

        _time.Advance(TwentyFive - TimeSpan.FromSeconds(1));
        Assert.Empty(_finished);

        _time.Advance(TimeSpan.FromSeconds(1));
        var finished = Assert.Single(_finished);
        Assert.Equal(taskId, finished.Session.TaskId);
        Assert.Equal(TimeSpan.Zero, finished.LateBy);
        Assert.Equal(FocusTimerState.Idle, _timer.State);

        _time.Advance(Minutes(10));
        _timer.CheckNow();
        Assert.Single(_finished);
    }

    [Fact]
    public void ATickAFewMillisecondsEarly_StillFinishesOnTime()
    {
        // Real Windows timers and the wall clock can disagree by a millisecond
        // or so; found by running the app (M5 step 5b).
        _timer.Start(TimeSpan.FromSeconds(10));
        _time.Advance(TimeSpan.FromSeconds(10) - TimeSpan.FromMilliseconds(1));

        _timer.CheckNow(); // as if the last tick fired 1 ms early

        Assert.Equal(TimeSpan.Zero, Assert.Single(_finished).LateBy);
    }

    [Fact]
    public void WellBeforeTheEnd_DoesNotFinishEarly()
    {
        _timer.Start(TimeSpan.FromSeconds(10));
        _time.Advance(TimeSpan.FromSeconds(9.5));

        _timer.CheckNow();

        Assert.Empty(_finished);
    }

    [Fact]
    public void AfterFinishing_NothingTicks()
    {
        _timer.Start(TimeSpan.FromSeconds(3));
        _time.Advance(TimeSpan.FromSeconds(3));
        var ticksAtFinish = _ticks;

        _time.Advance(Minutes(1));

        Assert.Equal(ticksAtFinish, _ticks);
    }

    [Fact]
    public void SleepJump_PastTheEnd_FinishesOnce_AndReportsHowLate()
    {
        // The PC sleeps 10 minutes into a 25-minute session and wakes 40 minutes later.
        _timer.Start(TwentyFive);
        _time.Advance(Minutes(10));

        _time.SetUtcNow(_time.GetUtcNow() + Minutes(40));
        _timer.CheckNow(); // the app also checks on wake

        var finished = Assert.Single(_finished);
        Assert.Equal(Minutes(25), finished.LateBy); // ended at 25, noticed at 50
    }

    [Fact]
    public void SleepJump_NotPastTheEnd_JustShowsLessTime()
    {
        _timer.Start(TwentyFive);

        _time.SetUtcNow(_time.GetUtcNow() + Minutes(10)); // a 10-minute sleep
        _timer.CheckNow();

        Assert.Empty(_finished);
        Assert.Equal(Minutes(15), _timer.Remaining);
    }

    // --- Pause / resume -----------------------------------------------------------

    [Fact]
    public void Pause_FreezesRemaining_AndStopsTicking()
    {
        _timer.Start(TwentyFive);
        _time.Advance(Minutes(10));

        _timer.Pause();
        var ticksAtPause = _ticks;
        _time.Advance(Minutes(30));

        Assert.Equal(FocusTimerState.Paused, _timer.State);
        Assert.Equal(Minutes(15), _timer.Remaining);
        Assert.Equal(ticksAtPause, _ticks);
        Assert.Empty(_finished);
    }

    [Fact]
    public void Resume_ContinuesWhereItLeftOff()
    {
        _timer.Start(TwentyFive);
        _time.Advance(Minutes(10));
        _timer.Pause();
        _time.Advance(Minutes(30));

        _timer.Resume();

        Assert.Equal(FocusTimerState.Running, _timer.State);
        Assert.Equal(Minutes(15), _timer.Remaining);

        _time.Advance(Minutes(15));
        Assert.Single(_finished);
    }

    [Fact]
    public void SeveralPauses_AddUp()
    {
        _timer.Start(Minutes(10));
        _time.Advance(Minutes(2));
        _timer.Pause();
        _time.Advance(Minutes(5));
        _timer.Resume();
        _time.Advance(Minutes(3));
        _timer.Pause();
        _time.Advance(Minutes(7));
        _timer.Resume();

        Assert.Equal(Minutes(5), _timer.Remaining); // 2 + 3 focused
        Assert.Equal(Minutes(12), _timer.Session!.PausedTotal);
    }

    [Fact]
    public void AfterAPartSecondPause_TicksLineUpWithWholeSeconds_AndZeroIsExact()
    {
        _timer.Start(TimeSpan.FromSeconds(10));
        _time.Advance(TimeSpan.FromSeconds(2.5));   // 7.5 s left
        _timer.Pause();
        _time.Advance(TimeSpan.FromSeconds(1.3));
        _timer.Resume();

        _time.Advance(TimeSpan.FromSeconds(0.5));   // first tick: 7.0 s left
        Assert.Equal(TimeSpan.FromSeconds(7), _timer.Remaining);

        _time.Advance(TimeSpan.FromSeconds(7));
        Assert.Equal(TimeSpan.Zero, Assert.Single(_finished).LateBy);
    }

    [Fact]
    public void PauseTwice_ResumeTwice_AreHarmless()
    {
        _timer.Start(TwentyFive);
        _timer.Pause();
        _timer.Pause();
        _timer.Resume();
        _timer.Resume();

        Assert.Equal(3, _stateChanges); // start, pause, resume
    }

    // --- Stop and replace -----------------------------------------------------------

    [Fact]
    public void Stop_EndsWithoutFinishing()
    {
        _timer.Start(TwentyFive);

        _timer.Stop();
        _time.Advance(Minutes(30));

        Assert.Equal(FocusTimerState.Idle, _timer.State);
        Assert.Empty(_finished);
    }

    [Fact]
    public void Start_WhileRunning_ReplacesTheSession()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        _timer.Start(TwentyFive, first);
        _time.Advance(Minutes(20));

        _timer.Start(Minutes(15), second);
        _time.Advance(Minutes(10)); // the first one would have ended here

        Assert.Empty(_finished);
        Assert.Equal(second, _timer.Session!.TaskId);
        Assert.Equal(Minutes(5), _timer.Remaining);
    }
}
