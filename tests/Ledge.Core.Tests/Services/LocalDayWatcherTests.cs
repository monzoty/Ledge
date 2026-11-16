using Ledge.Core.Services;
using Microsoft.Extensions.Time.Testing;

namespace Ledge.Core.Tests.Services;

public sealed class LocalDayWatcherTests : IDisposable
{
    private readonly FakeTimeProvider _time = TestClock.Create(TestClock.Local(2026, 11, 16, 23, 0));
    private readonly LocalDayWatcher _watcher;
    private int _dayChanges;

    public LocalDayWatcherTests()
    {
        _watcher = new LocalDayWatcher(_time);
        _watcher.DayChanged += (_, _) => _dayChanges++;
    }

    public void Dispose() => _watcher.Dispose();

    [Fact]
    public void Today_IsTheLocalDate()
    {
        Assert.Equal(TestClock.Day16, _watcher.Today);
    }

    [Fact]
    public void NothingHappens_BeforeMidnight()
    {
        _time.Advance(TimeSpan.FromMinutes(59)); // 23:59

        Assert.Equal(0, _dayChanges);
    }

    [Fact]
    public void DayChanged_FiresAtLocalMidnight()
    {
        _time.Advance(TimeSpan.FromHours(1)); // 00:00 on the 17th

        Assert.Equal(1, _dayChanges);
        Assert.Equal(TestClock.Day16.AddDays(1), _watcher.Today);
    }

    [Fact]
    public void DayChanged_FiresAgainTheNextMidnight()
    {
        _time.Advance(TimeSpan.FromHours(1));
        _time.Advance(TimeSpan.FromHours(23));

        Assert.Equal(1, _dayChanges);

        _time.Advance(TimeSpan.FromHours(1)); // 00:00 on the 18th

        Assert.Equal(2, _dayChanges);
    }

    [Fact]
    public void Check_OnSameDay_DoesNothing()
    {
        _watcher.Check();

        Assert.Equal(0, _dayChanges);
    }

    [Fact]
    public void Check_AfterClockJump_FiresOnce()
    {
        // e.g. the user moved the clock forward by three days.
        _time.SetUtcNow(_time.GetUtcNow().AddDays(3));

        // SetUtcNow fires the overdue midnight timer by itself; Check must not
        // report the same change a second time.
        _watcher.Check();

        Assert.Equal(1, _dayChanges);
        Assert.Equal(TestClock.Day16.AddDays(3), _watcher.Today);
    }

    [Fact]
    public void Dispose_StopsTheTimer()
    {
        _watcher.Dispose();

        _time.Advance(TimeSpan.FromHours(2));

        Assert.Equal(0, _dayChanges);
    }
}
