using Ledge.Core.Models;
using Ledge.Core.Services;
using Ledge.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace Ledge.Tests.ViewModels;

public sealed class PillViewModelTests : IDisposable
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.Zero));
    private readonly FocusTimer _focusTimer;
    private readonly TaskBoard _board;
    private readonly TimerViewModel _timer;
    private readonly PillViewModel _pill;

    public PillViewModelTests()
    {
        _focusTimer = new FocusTimer(_time);
        _board = new TaskBoard(_time);
        _timer = new TimerViewModel(_focusTimer, uiThread: null);
        var tasks = new TaskListViewModel(
            _board, _time, new TaskListMotion(TimeSpan.Zero), new TaskLoadResult(new TaskDocument(), TaskLoadOutcome.Loaded), _timer);
        _pill = new PillViewModel(tasks, _timer);
    }

    public void Dispose() => _focusTimer.Dispose();

    [Fact]
    public void NothingOpen_NoTimer_IsAllClear()
    {
        Assert.Equal(PillMode.AllClear, _pill.Mode);
    }

    [Fact]
    public void OpenTasks_ShowTheCount()
    {
        _board.Add("Task");

        Assert.Equal(PillMode.TodayCount, _pill.Mode);
    }

    [Fact]
    public void RunningTimer_WinsOverTheCount()
    {
        _board.Add("Task");

        _timer.StartCommand.Execute(null);

        Assert.Equal(PillMode.Timer, _pill.Mode);
    }

    [Fact]
    public void PausedTimer_StillShowsTheTimer()
    {
        _timer.StartCommand.Execute(null);

        _timer.PauseCommand.Execute(null);

        Assert.Equal(PillMode.Timer, _pill.Mode);
    }

    [Fact]
    public void WhenTheTimerEnds_ItGoesBackToTheCount()
    {
        _board.Add("Task");
        _timer.StartCommand.Execute(null);

        _time.Advance(TimeSpan.FromMinutes(25));

        Assert.Equal(PillMode.TodayCount, _pill.Mode);
    }

    [Fact]
    public void Progress_IsTheFractionLeft()
    {
        _timer.StartCommand.Execute(null);
        Assert.Equal(1, _timer.Progress, precision: 3);

        _time.Advance(TimeSpan.FromMinutes(5)); // 20 of 25 left

        Assert.Equal(0.8, _timer.Progress, precision: 3);
    }
}
