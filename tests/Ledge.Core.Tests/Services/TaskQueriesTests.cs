using Ledge.Core.Models;
using Ledge.Core.Services;

namespace Ledge.Core.Tests.Services;

public sealed class TaskQueriesTests
{
    private static int _nextSortOrder;

    private static TaskItem Task(
        DateOnly? plannedFor,
        bool isDone = false,
        DateTimeOffset? completedAt = null,
        int? sortOrder = null) => new()
        {
            Id = Guid.NewGuid(),
            Title = "Task",
            PlannedFor = plannedFor,
            IsDone = isDone,
            CompletedAt = completedAt,
            CreatedAt = TestClock.Local(2026, 11, 1, 9, 0),
            SortOrder = sortOrder ?? Interlocked.Increment(ref _nextSortOrder),
        };

    private static TaskDocument Doc(params TaskItem[] tasks) => new() { Tasks = tasks };

    [Fact]
    public void CurrentDate_UsesLocalTimeZone()
    {
        // 20:00 UTC on the 16th is already 01:30 on the 17th in UTC+05:30.
        var time = TestClock.Create(new DateTimeOffset(2026, 11, 16, 20, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 11, 17), TaskQueries.CurrentDate(time));
    }

    [Fact]
    public void Today_ContainsOpenTaskPlannedForToday()
    {
        var task = Task(TestClock.Day16);

        var today = TaskQueries.Today(Doc(task), TestClock.Create());

        Assert.Equal([task], today);
    }

    [Fact]
    public void Today_RollsOverOpenTaskFromEarlierDay()
    {
        var task = Task(TestClock.Day16.AddDays(-3));

        Assert.Equal([task], TaskQueries.Today(Doc(task), TestClock.Create()));
    }

    [Fact]
    public void Today_ExcludesTaskPlannedForTomorrow()
    {
        var task = Task(TestClock.Day16.AddDays(1));

        Assert.Empty(TaskQueries.Today(Doc(task), TestClock.Create()));
    }

    [Fact]
    public void Today_ContainsPlannedTaskCompletedToday()
    {
        var task = Task(TestClock.Day16.AddDays(-2), isDone: true, completedAt: TestClock.Local(2026, 11, 16, 8, 0));

        Assert.Equal([task], TaskQueries.Today(Doc(task), TestClock.Create()));
    }

    [Fact]
    public void DoneTaskFromEarlierDay_IsHiddenFromBothLists()
    {
        var planned = Task(TestClock.Day16.AddDays(-1), isDone: true, completedAt: TestClock.Local(2026, 11, 15, 18, 0));
        var inbox = Task(null, isDone: true, completedAt: TestClock.Local(2026, 11, 15, 18, 0));
        var doc = Doc(planned, inbox);
        var time = TestClock.Create();

        Assert.Empty(TaskQueries.Today(doc, time));
        Assert.Empty(TaskQueries.Inbox(doc, time));
    }

    [Fact]
    public void DoneTaskWithoutCompletedAt_IsHidden()
    {
        // Shouldn't happen, but a hand-edited file could contain it.
        var task = Task(TestClock.Day16, isDone: true, completedAt: null);

        Assert.Empty(TaskQueries.Today(Doc(task), TestClock.Create()));
    }

    [Fact]
    public void Inbox_ContainsOpenTaskWithoutDate_AndTodayDoesNot()
    {
        var task = Task(null);
        var time = TestClock.Create();

        Assert.Equal([task], TaskQueries.Inbox(Doc(task), time));
        Assert.Empty(TaskQueries.Today(Doc(task), time));
    }

    [Fact]
    public void InboxTaskCompletedToday_StaysInInboxOnly()
    {
        var task = Task(null, isDone: true, completedAt: TestClock.Local(2026, 11, 16, 9, 0));
        var time = TestClock.Create();

        Assert.Equal([task], TaskQueries.Inbox(Doc(task), time));
        Assert.Empty(TaskQueries.Today(Doc(task), time));
    }

    [Fact]
    public void Lists_AreSortedBySortOrder()
    {
        var third = Task(TestClock.Day16, sortOrder: 30);
        var first = Task(TestClock.Day16, sortOrder: 10);
        var second = Task(TestClock.Day16, sortOrder: 20);

        var today = TaskQueries.Today(Doc(third, first, second), TestClock.Create());

        Assert.Equal([first, second, third], today);
    }

    [Fact]
    public void Midnight_DoneTasksDisappear_OpenTasksRollOver()
    {
        var time = TestClock.Create(TestClock.Local(2026, 11, 16, 23, 59));
        var open = Task(TestClock.Day16);
        var done = Task(TestClock.Day16, isDone: true, completedAt: TestClock.Local(2026, 11, 16, 23, 58));
        var doc = Doc(open, done);

        Assert.Equal([open, done], TaskQueries.Today(doc, time));

        time.Advance(TimeSpan.FromMinutes(2)); // 00:01 on the 17th

        Assert.Equal([open], TaskQueries.Today(doc, time));
    }

    [Fact]
    public void CompletedOn_UsesLocalDate_NotUtcDate()
    {
        // Completed at 00:30 local on the 17th = 19:00 UTC on the 16th.
        var time = TestClock.Create(TestClock.Local(2026, 11, 17, 2, 0));
        var task = Task(null, isDone: true, completedAt: new DateTimeOffset(2026, 11, 16, 19, 0, 0, TimeSpan.Zero));

        Assert.Equal([task], TaskQueries.Inbox(Doc(task), time));
    }
}
