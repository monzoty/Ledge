using Ledge.Core.Models;
using Ledge.Core.Services;
using Ledge.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace Ledge.Tests.ViewModels;

public sealed class TimerViewModelTests : IDisposable
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.Zero));
    private readonly FocusTimer _timer;
    private readonly TimerViewModel _viewModel;

    public TimerViewModelTests()
    {
        _timer = new FocusTimer(_time);
        _viewModel = new TimerViewModel(_timer, uiThread: null);
    }

    public void Dispose() => _timer.Dispose();

    [Theory]
    [InlineData(25 * 60, "25:00")]
    [InlineData(59, "00:59")]
    [InlineData(0.4, "00:01")]   // rounds up: never 00:00 while time is left
    [InlineData(0, "00:00")]
    [InlineData(50 * 60, "50:00")]
    [InlineData((24 * 60) + 59.0005, "24:59")] // a tick 0.5 ms early must not show 25:00
    [InlineData(59.05, "00:59")]
    public void Format_IsMinutesAndSeconds(double seconds, string expected)
    {
        Assert.Equal(expected, TimerViewModel.Format(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void StartsIdle_With25Selected()
    {
        Assert.True(_viewModel.IsIdle);
        Assert.Equal(25, _viewModel.SelectedMinutes);
        Assert.Equal(string.Empty, _viewModel.RemainingText);
        Assert.Null(_viewModel.LinkedTaskId);
    }

    [Fact]
    public void SelectPreset_ChangesSelection_OnlyToKnownPresets()
    {
        _viewModel.SelectPresetCommand.Execute(50);
        Assert.Equal(50, _viewModel.SelectedMinutes);

        _viewModel.SelectPresetCommand.Execute(7);
        Assert.Equal(50, _viewModel.SelectedMinutes);
    }

    [Fact]
    public void Start_UsesSelectedPreset_Unlinked()
    {
        _viewModel.SelectPresetCommand.Execute(15);

        _viewModel.StartCommand.Execute(null);

        Assert.True(_viewModel.IsRunning);
        Assert.Equal("15:00", _viewModel.RemainingText);
        Assert.Null(_viewModel.LinkedTaskId);
    }

    [Fact]
    public void RemainingText_UpdatesEverySecond()
    {
        _viewModel.StartCommand.Execute(null);

        _time.Advance(TimeSpan.FromSeconds(78));

        Assert.Equal("23:42", _viewModel.RemainingText);
    }

    [Fact]
    public void PauseResumeStop_UpdateState()
    {
        _viewModel.StartCommand.Execute(null);

        _viewModel.PauseCommand.Execute(null);
        Assert.True(_viewModel.IsPaused);

        _viewModel.ResumeCommand.Execute(null);
        Assert.True(_viewModel.IsRunning);

        _viewModel.StopCommand.Execute(null);
        Assert.True(_viewModel.IsIdle);
        Assert.Equal(string.Empty, _viewModel.RemainingText);
    }

    [Fact]
    public void RequestStart_WhenIdle_StartsLinkedAtOnce()
    {
        var taskId = Guid.NewGuid();

        _viewModel.RequestStart(taskId);

        Assert.True(_viewModel.IsRunning);
        Assert.Equal(taskId, _viewModel.LinkedTaskId);
        Assert.False(_viewModel.IsAskingToReplace);
    }

    [Fact]
    public void RequestStart_WhileRunning_AsksFirst_AndKeepsTheOldSession()
    {
        var first = Guid.NewGuid();
        _viewModel.RequestStart(first);

        _viewModel.RequestStart(Guid.NewGuid());

        Assert.True(_viewModel.IsAskingToReplace);
        Assert.Equal(first, _viewModel.LinkedTaskId);
    }

    [Fact]
    public void ConfirmReplace_StartsTheNewSession()
    {
        _viewModel.RequestStart(Guid.NewGuid());
        _time.Advance(TimeSpan.FromMinutes(10));
        var second = Guid.NewGuid();
        _viewModel.RequestStart(second);

        _viewModel.ConfirmReplaceCommand.Execute(null);

        Assert.False(_viewModel.IsAskingToReplace);
        Assert.Equal(second, _viewModel.LinkedTaskId);
        Assert.Equal("25:00", _viewModel.RemainingText);
    }

    [Fact]
    public void KeepCurrent_LeavesTheSessionAlone()
    {
        var first = Guid.NewGuid();
        _viewModel.RequestStart(first);
        _viewModel.RequestStart(Guid.NewGuid());

        _viewModel.KeepCurrentCommand.Execute(null);

        Assert.False(_viewModel.IsAskingToReplace);
        Assert.Equal(first, _viewModel.LinkedTaskId);
    }

    [Fact]
    public void RequestStart_WhilePaused_AlsoAsks()
    {
        _viewModel.StartCommand.Execute(null);
        _viewModel.PauseCommand.Execute(null);

        _viewModel.RequestStart(Guid.NewGuid());

        Assert.True(_viewModel.IsAskingToReplace);
    }

    [Fact]
    public void SessionEndingWhileAsking_DropsTheQuestion()
    {
        _viewModel.StartCommand.Execute(null);
        _viewModel.RequestStart(Guid.NewGuid());

        _time.Advance(TimeSpan.FromMinutes(25)); // finishes

        Assert.False(_viewModel.IsAskingToReplace);
        Assert.True(_viewModel.IsIdle);
    }

    [Fact]
    public void ConfirmReplace_WithoutAQuestion_DoesNothing()
    {
        _viewModel.ConfirmReplaceCommand.Execute(null);

        Assert.True(_viewModel.IsIdle);
    }

    [Fact]
    public void Finishing_ClearsTheLink()
    {
        _viewModel.RequestStart(Guid.NewGuid());

        _time.Advance(TimeSpan.FromMinutes(25));

        Assert.True(_viewModel.IsIdle);
        Assert.Null(_viewModel.LinkedTaskId);
        Assert.Equal(FocusTimerState.Idle, _viewModel.State);
    }
}
