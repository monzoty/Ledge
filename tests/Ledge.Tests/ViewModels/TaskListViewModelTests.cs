using Ledge.Core.Models;
using Ledge.Core.Services;
using Ledge.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace Ledge.Tests.ViewModels;

public sealed class TaskListViewModelTests
{
    private static readonly TimeSpan RemoveDuration = TimeSpan.FromMilliseconds(120);

    // 16 Nov 2026, 10:00 UTC. The local zone stays UTC here: time-zone rules
    // are already covered by the Core tests.
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero));
    private readonly TaskBoard _board;
    private readonly TimerViewModel _timer;
    private readonly TaskListViewModel _viewModel;

    public TaskListViewModelTests()
    {
        _board = new TaskBoard(_time);
        _timer = new TimerViewModel(new FocusTimer(_time), uiThread: null);
        _viewModel = CreateViewModel();
    }

    private IEnumerable<string> TodayTitles => _viewModel.Today.Select(row => row.Title);

    private TaskListViewModel CreateViewModel(TaskLoadOutcome outcome = TaskLoadOutcome.Loaded) =>
        new(_board, _time, new TaskListMotion(RemoveDuration), new TaskLoadResult(_board.Document, outcome), _timer);

    private TaskRowViewModel AddRow(string title)
    {
        var task = _board.Add(title)!;
        return _viewModel.Today.Single(row => row.Id == task.Id);
    }

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

        var viewModel = CreateViewModel();

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
        var row = AddRow("One");
        AddRow("Two");

        row.ToggleCommand.Execute(null);

        Assert.True(row.IsDone);
        Assert.Equal(1, _viewModel.TodayOpenCount);
        Assert.Equal(2, _viewModel.Today.Count); // done rows stay visible
    }

    [Fact]
    public void Toggle_UpdatesTheSameRowObject()
    {
        // The row must be updated in place, not replaced: that's what lets the
        // "done" animation play on the row that's already on screen.
        var rowBefore = AddRow("Task");

        rowBefore.ToggleCommand.Execute(null);

        Assert.Same(rowBefore, _viewModel.Today[0]);
    }

    [Fact]
    public void Toggle_RaisesPropertyChangedForIsDone()
    {
        var row = AddRow("Task");
        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        row.ToggleCommand.Execute(null);

        Assert.Equal([nameof(TaskRowViewModel.IsDone)], changed);
    }

    // --- Sync with the board ----------------------------------------------------

    [Fact]
    public void RemovedTask_RemovesItsRow_KeepsOthers()
    {
        var a = _board.Add("A")!;
        var rowB = AddRow("B");

        _board.Delete(a.Id);

        Assert.Equal(["B"], TodayTitles);
        Assert.Same(rowB, _viewModel.Today[0]);
    }

    [Fact]
    public void RenamedTask_UpdatesTitleInPlace()
    {
        var row = AddRow("Old");

        _board.Rename(row.Id, "New");

        Assert.Same(row, _viewModel.Today[0]);
        Assert.Equal("New", row.Title);
    }

    // --- Inbox and move ---------------------------------------------------------

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
        AddRow("Stay");
        var go = AddRow("Go");

        go.MoveCommand.Execute(null);

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

        _viewModel.Inbox[0].MoveCommand.Execute(null);

        Assert.Equal(["Task"], TodayTitles);
        Assert.False(_viewModel.Today[0].IsInInbox);
        Assert.True(_viewModel.IsInboxEmpty);
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

    // --- Delete and undo --------------------------------------------------------

    [Fact]
    public void Delete_PlaysAnimationFirst_ThenRemovesRow()
    {
        var row = AddRow("Task");

        var deleting = row.DeleteCommand.ExecuteAsync(null);

        // During the 120 ms animation the row is still there, marked as removing.
        Assert.True(row.IsRemoving);
        Assert.Single(_viewModel.Today);
        Assert.False(_viewModel.ShowUndoHint);

        _time.Advance(RemoveDuration);
        Assert.True(deleting.IsCompleted);

        Assert.Empty(_viewModel.Today);
        Assert.True(_viewModel.ShowUndoHint);
    }

    [Fact]
    public void RowBeingRemoved_IgnoresOtherCommands()
    {
        var row = AddRow("Task");

        var deleting = row.DeleteCommand.ExecuteAsync(null);

        Assert.False(row.ToggleCommand.CanExecute(null));
        Assert.False(row.MoveCommand.CanExecute(null));
        Assert.False(row.BeginRenameCommand.CanExecute(null));

        // Let the delete finish: xUnit waits for unfinished async work at the end
        // of a test, so leaving the fake clock stopped here would hang the run.
        _time.Advance(RemoveDuration);
        Assert.True(deleting.IsCompleted);
    }

    [Fact]
    public void UndoDelete_RestoresTask_AndHidesHint()
    {
        AddRow("A");
        var b = AddRow("B");
        AddRow("C");
        _ = b.DeleteCommand.ExecuteAsync(null);
        _time.Advance(RemoveDuration);

        _viewModel.UndoDeleteCommand.Execute(null);

        Assert.Equal(["A", "B", "C"], TodayTitles);
        Assert.False(_viewModel.ShowUndoHint);
        Assert.False(_viewModel.Today[1].IsRemoving); // a fresh row, not the faded one
    }

    [Fact]
    public void ClearUndo_HidesHint_AndUndoDoesNothing()
    {
        var row = AddRow("Task");
        _ = row.DeleteCommand.ExecuteAsync(null);
        _time.Advance(RemoveDuration);

        _viewModel.ClearUndo();
        _viewModel.UndoDeleteCommand.Execute(null);

        Assert.False(_viewModel.ShowUndoHint);
        Assert.Empty(_viewModel.Today);
    }

    // --- Rename -------------------------------------------------------------------

    [Fact]
    public void BeginRename_OpensBoxWithCurrentTitle()
    {
        var row = AddRow("Email Priya");

        row.BeginRenameCommand.Execute(null);

        Assert.True(row.IsEditing);
        Assert.Equal("Email Priya", row.EditText);
        Assert.True(_viewModel.IsRenaming);
    }

    [Fact]
    public void CommitRename_SavesNormalizedTitle()
    {
        var row = AddRow("Old");
        row.BeginRenameCommand.Execute(null);
        row.EditText = "  New title ";

        row.CommitRenameCommand.Execute(null);

        Assert.False(row.IsEditing);
        Assert.False(_viewModel.IsRenaming);
        Assert.Equal("New title", row.Title);
    }

    [Fact]
    public void CommitRename_WithEmptyText_KeepsOldTitle()
    {
        var row = AddRow("Old");
        row.BeginRenameCommand.Execute(null);
        row.EditText = "   ";

        row.CommitRenameCommand.Execute(null);

        Assert.False(row.IsEditing);
        Assert.Equal("Old", row.Title);
    }

    [Fact]
    public void CancelRename_KeepsOldTitle()
    {
        var row = AddRow("Old");
        row.BeginRenameCommand.Execute(null);
        row.EditText = "Something else";

        row.CancelRenameCommand.Execute(null);

        Assert.False(row.IsEditing);
        Assert.False(_viewModel.IsRenaming);
        Assert.Equal("Old", row.Title);
    }

    [Fact]
    public void CommitAfterCancel_DoesNothing()
    {
        // Esc closes the box; the box then loses focus, which also commits.
        // That second call must not save the abandoned text.
        var row = AddRow("Old");
        row.BeginRenameCommand.Execute(null);
        row.EditText = "Abandoned";

        row.CancelRenameCommand.Execute(null);
        row.CommitRenameCommand.Execute(null);

        Assert.Equal("Old", row.Title);
    }

    [Fact]
    public void WhileRenaming_RowShortcutsAreDisabled()
    {
        // So typing a space in the rename box doesn't also tick the task.
        var row = AddRow("Task");

        row.BeginRenameCommand.Execute(null);

        Assert.False(row.ToggleCommand.CanExecute(null));
        Assert.False(row.DeleteCommand.CanExecute(null));
        Assert.False(row.MoveCommand.CanExecute(null));
        Assert.False(row.BeginRenameCommand.CanExecute(null));
    }

    // --- Pill label -------------------------------------------------------------

    [Fact]
    public void HasOpenToday_FollowsOpenCount()
    {
        Assert.False(_viewModel.HasOpenToday); // "All clear"

        var row = AddRow("Task");
        Assert.True(_viewModel.HasOpenToday); // "1 today"

        row.ToggleCommand.Execute(null);
        Assert.False(_viewModel.HasOpenToday); // last one done: "All clear" again
    }

    [Fact]
    public void HasOpenToday_IsAnnouncedWhenItChanges()
    {
        var changed = new List<string?>();
        _viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        AddRow("Task");

        Assert.Contains(nameof(TaskListViewModel.HasOpenToday), changed);
    }

    [Fact]
    public void InboxTasks_DoNotCountForThePill()
    {
        var task = _board.Add("Task")!;

        _board.MoveToInbox(task.Id);

        Assert.False(_viewModel.HasOpenToday);
    }

    // --- Data notices -------------------------------------------------------------

    [Fact]
    public void NormalLoad_ShowsNoNotice()
    {
        Assert.False(_viewModel.ShowDataNotice);
        Assert.False(_viewModel.ShowRestoredNotice);
        Assert.False(_viewModel.ShowResetNotice);
    }

    [Fact]
    public void RestoredFromBackup_ShowsRestoredNotice()
    {
        var viewModel = CreateViewModel(TaskLoadOutcome.RestoredFromBackup);

        Assert.True(viewModel.ShowDataNotice);
        Assert.True(viewModel.ShowRestoredNotice);
        Assert.False(viewModel.ShowResetNotice);
    }

    [Fact]
    public void ResetAfterCorruption_ShowsResetNotice()
    {
        var viewModel = CreateViewModel(TaskLoadOutcome.ResetAfterCorruption);

        Assert.True(viewModel.ShowDataNotice);
        Assert.False(viewModel.ShowRestoredNotice);
        Assert.True(viewModel.ShowResetNotice);
    }

    [Fact]
    public void FirstRun_ShowsNoNotice()
    {
        Assert.False(CreateViewModel(TaskLoadOutcome.FirstRun).ShowDataNotice);
    }

    [Fact]
    public void OnPanelClosed_HidesNotice_AndClearsUndo()
    {
        var viewModel = CreateViewModel(TaskLoadOutcome.RestoredFromBackup);
        _board.Add("Task");
        _ = viewModel.Today[0].DeleteCommand.ExecuteAsync(null);
        _time.Advance(RemoveDuration);
        Assert.True(viewModel.ShowUndoHint);

        viewModel.OnPanelClosed();

        Assert.False(viewModel.ShowDataNotice);
        Assert.False(viewModel.ShowUndoHint);
        Assert.False(_board.CanUndoDelete);
    }

    // --- Focus timer link ------------------------------------------------------------

    [Fact]
    public void StartFocus_StartsATimerLinkedToTheRow_AndMarksIt()
    {
        var other = AddRow("Other");
        var row = AddRow("Focus on me");

        row.StartFocusCommand.Execute(null);

        Assert.True(_timer.IsRunning);
        Assert.Equal(row.Id, _timer.LinkedTaskId);
        Assert.True(row.IsLinkedToTimer);
        Assert.False(other.IsLinkedToTimer);
    }

    [Fact]
    public void StoppingTheTimer_RemovesTheMark()
    {
        var row = AddRow("Task");
        row.StartFocusCommand.Execute(null);

        _timer.StopCommand.Execute(null);

        Assert.False(row.IsLinkedToTimer);
    }

    [Fact]
    public void StartFocus_OnAnotherRowWhileRunning_AsksFirst()
    {
        var first = AddRow("First");
        var second = AddRow("Second");
        first.StartFocusCommand.Execute(null);

        second.StartFocusCommand.Execute(null);

        Assert.True(_timer.IsAskingToReplace);
        Assert.True(first.IsLinkedToTimer);

        _timer.ConfirmReplaceCommand.Execute(null);

        Assert.False(first.IsLinkedToTimer);
        Assert.True(second.IsLinkedToTimer);
    }

    [Fact]
    public void LinkedRow_KeepsItsMark_AfterBeingMovedToTheInbox()
    {
        var row = AddRow("Task");
        row.StartFocusCommand.Execute(null);

        row.MoveCommand.Execute(null);

        Assert.True(_viewModel.Inbox[0].IsLinkedToTimer); // a new row object in the Inbox
    }

    [Fact]
    public void WhileRenaming_CtrlEnterIsDisabled()
    {
        var row = AddRow("Task");

        row.BeginRenameCommand.Execute(null);

        Assert.False(row.StartFocusCommand.CanExecute(null));
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
