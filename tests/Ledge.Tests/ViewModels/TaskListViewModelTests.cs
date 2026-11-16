using Ledge.Core.Services;
using Ledge.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace Ledge.Tests.ViewModels;

public sealed class TaskListViewModelTests
{
    // 16 Nov 2026, 10:00 UTC. The local zone stays UTC here: time-zone rules
    // are already covered by the Core tests.
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero));
    private readonly TaskBoard _board;
    private readonly TaskListViewModel _viewModel;

    public TaskListViewModelTests()
    {
        _board = new TaskBoard(_time);
        _viewModel = new TaskListViewModel(_board, _time);
    }

    private IEnumerable<string> TodayTitles => _viewModel.Today.Select(row => row.Title);

    // --- Start-up --------------------------------------------------------------

    [Fact]
    public void EmptyBoard_ShowsEmptyState()
    {
        Assert.Empty(_viewModel.Today);
        Assert.True(_viewModel.IsTodayEmpty);
        Assert.Equal(0, _viewModel.TodayOpenCount);
    }

    [Fact]
    public void ExistingTasks_AreShownAtStart()
    {
        _board.Add("One");
        _board.Add("Two");

        var viewModel = new TaskListViewModel(_board, _time);

        Assert.Equal(["One", "Two"], viewModel.Today.Select(row => row.Title));
        Assert.False(viewModel.IsTodayEmpty);
    }

    // --- Quick-add -------------------------------------------------------------

    [Fact]
    public void Add_AddsRow_AndClearsBox()
    {
        _viewModel.QuickAddText = "  Email Priya ";

        _viewModel.AddCommand.Execute(null);

        Assert.Equal(["Email Priya"], TodayTitles);
        Assert.Equal(string.Empty, _viewModel.QuickAddText);
        Assert.False(_viewModel.IsTodayEmpty);
        Assert.Equal(1, _viewModel.TodayOpenCount);
    }

    [Fact]
    public void Add_WithBlankText_KeepsTextAndAddsNothing()
    {
        _viewModel.QuickAddText = "   ";

        _viewModel.AddCommand.Execute(null);

        Assert.Empty(_viewModel.Today);
        Assert.Equal("   ", _viewModel.QuickAddText);
    }

    [Fact]
    public void NewRows_AreAddedAtTheBottom()
    {
        _board.Add("First");
        _board.Add("Second");

        Assert.Equal(["First", "Second"], TodayTitles);
    }

    // --- Toggle ----------------------------------------------------------------

    [Fact]
    public void Toggle_MarksRowDone_AndUpdatesOpenCount()
    {
        _board.Add("One");
        _board.Add("Two");
        var row = _viewModel.Today[0];

        _viewModel.ToggleCommand.Execute(row);

        Assert.True(row.IsDone);
        Assert.Equal(1, _viewModel.TodayOpenCount);
        Assert.Equal(2, _viewModel.Today.Count); // done rows stay visible
    }

    [Fact]
    public void Toggle_UpdatesTheSameRowObject()
    {
        // The row must be updated in place, not replaced: that's what lets the
        // "done" animation play on the row that's already on screen.
        _board.Add("Task");
        var rowBefore = _viewModel.Today[0];

        _viewModel.ToggleCommand.Execute(rowBefore);

        Assert.Same(rowBefore, _viewModel.Today[0]);
    }

    [Fact]
    public void Toggle_RaisesPropertyChangedForIsDone()
    {
        _board.Add("Task");
        var row = _viewModel.Today[0];
        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _viewModel.ToggleCommand.Execute(row);

        Assert.Equal([nameof(TaskRowViewModel.IsDone)], changed);
    }

    [Fact]
    public void Toggle_WithNull_DoesNothing()
    {
        _board.Add("Task");

        _viewModel.ToggleCommand.Execute(null);

        Assert.False(_viewModel.Today[0].IsDone);
    }

    // --- Sync with the board ----------------------------------------------------

    [Fact]
    public void RemovedTask_RemovesItsRow_KeepsOthers()
    {
        var a = _board.Add("A")!;
        _board.Add("B");
        var rowB = _viewModel.Today[1];

        _board.Delete(a.Id);

        Assert.Equal(["B"], TodayTitles);
        Assert.Same(rowB, _viewModel.Today[0]);
    }

    [Fact]
    public void RestoredTask_ReturnsToItsPosition()
    {
        _board.Add("A");
        var b = _board.Add("B")!;
        _board.Add("C");

        _board.Delete(b.Id);
        _board.UndoDelete();

        Assert.Equal(["A", "B", "C"], TodayTitles);
    }

    [Fact]
    public void RenamedTask_UpdatesTitleInPlace()
    {
        var task = _board.Add("Old")!;
        var row = _viewModel.Today[0];

        _board.Rename(task.Id, "New");

        Assert.Same(row, _viewModel.Today[0]);
        Assert.Equal("New", row.Title);
    }

    [Fact]
    public void TaskMovedToInbox_LeavesToday()
    {
        var task = _board.Add("Task")!;

        _board.MoveToInbox(task.Id);

        Assert.Empty(_viewModel.Today);
        Assert.True(_viewModel.IsTodayEmpty);
    }

    // --- Inbox ------------------------------------------------------------------

    [Fact]
    public void Inbox_StartsCollapsedAndEmpty()
    {
        Assert.False(_viewModel.IsInboxExpanded);
        Assert.True(_viewModel.IsInboxEmpty);
        Assert.False(_viewModel.ShowInboxEmpty); // collapsed: no "Inbox is clear."
    }

    [Fact]
    public void ToggleInbox_OpensAndCloses_AndUpdatesEmptyText()
    {
        var changed = new List<string?>();
        _viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _viewModel.ToggleInboxCommand.Execute(null);

        Assert.True(_viewModel.IsInboxExpanded);
        Assert.True(_viewModel.ShowInboxEmpty);
        Assert.Contains(nameof(TaskListViewModel.ShowInboxEmpty), changed);

        _viewModel.ToggleInboxCommand.Execute(null);

        Assert.False(_viewModel.IsInboxExpanded);
    }

    [Fact]
    public void Move_TodayRow_GoesToBottomOfInbox()
    {
        var inboxTask = _board.Add("Already in inbox")!;
        _board.MoveToInbox(inboxTask.Id);
        _board.Add("Stay");
        _board.Add("Go");

        _viewModel.MoveCommand.Execute(_viewModel.Today.Single(row => row.Title == "Go"));

        Assert.Equal(["Stay"], TodayTitles);
        Assert.Equal(["Already in inbox", "Go"], _viewModel.Inbox.Select(row => row.Title));
        Assert.True(_viewModel.Inbox[1].IsInInbox);
        Assert.Equal(2, _viewModel.InboxOpenCount);
        Assert.Equal(1, _viewModel.TodayOpenCount);
    }

    [Fact]
    public void Move_InboxRow_GoesBackToToday()
    {
        var task = _board.Add("Task")!;
        _board.MoveToInbox(task.Id);

        _viewModel.MoveCommand.Execute(_viewModel.Inbox[0]);

        Assert.Equal(["Task"], TodayTitles);
        Assert.False(_viewModel.Today[0].IsInInbox);
        Assert.Empty(_viewModel.Inbox);
        Assert.True(_viewModel.IsInboxEmpty);
    }

    [Fact]
    public void Move_WithNull_DoesNothing()
    {
        _board.Add("Task");

        _viewModel.MoveCommand.Execute(null);

        Assert.Single(_viewModel.Today);
    }

    [Fact]
    public void InboxOpenCount_IgnoresDoneTasks()
    {
        var a = _board.Add("A")!;
        var b = _board.Add("B")!;
        _board.MoveToInbox(a.Id);
        _board.MoveToInbox(b.Id);

        _board.Toggle(a.Id);

        Assert.Equal(2, _viewModel.Inbox.Count); // done stays visible today
        Assert.Equal(1, _viewModel.InboxOpenCount);
    }

    // --- Day change -------------------------------------------------------------

    [Fact]
    public void Refresh_AfterMidnight_DropsTasksDoneYesterday()
    {
        var done = _board.Add("Done")!;
        _board.Add("Open");
        _board.Toggle(done.Id);

        _time.Advance(TimeSpan.FromDays(1));
        _viewModel.Refresh();

        Assert.Equal(["Open"], TodayTitles);
        Assert.Equal(1, _viewModel.TodayOpenCount);
    }
}
