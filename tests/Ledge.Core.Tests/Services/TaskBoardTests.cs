using Ledge.Core.Models;
using Ledge.Core.Services;
using Microsoft.Extensions.Time.Testing;

namespace Ledge.Core.Tests.Services;

public sealed class TaskBoardTests
{
    private readonly FakeTimeProvider _time = TestClock.Create();
    private readonly TaskBoard _board;
    private int _changedCount;

    public TaskBoardTests()
    {
        _board = new TaskBoard(_time);
        _board.Changed += (_, _) => _changedCount++;
    }

    private IReadOnlyList<TaskItem> Today => TaskQueries.Today(_board.Document, _time);

    private IReadOnlyList<TaskItem> Inbox => TaskQueries.Inbox(_board.Document, _time);

    private TaskItem Get(Guid id) => Assert.Single(_board.Document.Tasks, task => task.Id == id);

    private TaskItem AddTask(string title = "Task")
    {
        var task = _board.Add(title);
        Assert.NotNull(task);
        return task;
    }

    // --- Constructor ---------------------------------------------------------

    [Fact]
    public void NewBoard_IsEmpty()
    {
        Assert.Empty(_board.Document.Tasks);
        Assert.Equal(TaskDocument.CurrentSchemaVersion, _board.Document.SchemaVersion);
    }

    [Fact]
    public void NewBoard_KeepsGivenDocument()
    {
        var document = new TaskDocument();

        Assert.Same(document, new TaskBoard(_time, document).Document);
    }

    // --- Add -----------------------------------------------------------------

    [Fact]
    public void Add_CreatesOpenTaskForToday()
    {
        var task = AddTask("  Write report  ");

        Assert.Equal("Write report", task.Title);
        Assert.Equal(TestClock.Day16, task.PlannedFor);
        Assert.False(task.IsDone);
        Assert.Null(task.CompletedAt);
        Assert.Equal(TestClock.DefaultStart, task.CreatedAt);
        Assert.NotEqual(Guid.Empty, task.Id);
        Assert.Equal([task], Today);
        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public void Add_PutsTaskAtBottomOfToday()
    {
        var first = AddTask("First");
        var second = AddTask("Second");

        Assert.Equal([first, second], Today);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Add_IgnoresEmptyTitle(string? title)
    {
        Assert.Null(_board.Add(title));
        Assert.Empty(_board.Document.Tasks);
        Assert.Equal(0, _changedCount);
    }

    [Fact]
    public void Add_CutsLongTitle()
    {
        var task = AddTask(new string('x', TaskTitle.MaxLength + 1));

        Assert.Equal(TaskTitle.MaxLength, task.Title.Length);
    }

    // --- Toggle --------------------------------------------------------------

    [Fact]
    public void Toggle_CompletesTask_AndRecordsWhen()
    {
        var task = AddTask();
        _time.Advance(TimeSpan.FromMinutes(5));

        Assert.True(_board.Toggle(task.Id));

        var done = Get(task.Id);
        Assert.True(done.IsDone);
        Assert.Equal(_time.GetUtcNow(), done.CompletedAt);
        Assert.Equal([done], Today); // still visible, struck through
        Assert.Equal(2, _changedCount);
    }

    [Fact]
    public void Toggle_Twice_UncompletesTask()
    {
        var task = AddTask();

        _board.Toggle(task.Id);
        _board.Toggle(task.Id);

        var reopened = Get(task.Id);
        Assert.False(reopened.IsDone);
        Assert.Null(reopened.CompletedAt);
    }

    [Fact]
    public void Toggle_UnknownId_DoesNothing()
    {
        AddTask();

        Assert.False(_board.Toggle(Guid.NewGuid()));
        Assert.Equal(1, _changedCount);
    }

    // --- Delete / undo -------------------------------------------------------

    [Fact]
    public void Delete_RemovesTask_AndEnablesUndo()
    {
        var task = AddTask();

        Assert.True(_board.Delete(task.Id));

        Assert.Empty(_board.Document.Tasks);
        Assert.True(_board.CanUndoDelete);
        Assert.Equal(2, _changedCount);
    }

    [Fact]
    public void UndoDelete_RestoresTaskInSamePosition()
    {
        var first = AddTask("First");
        var middle = AddTask("Middle");
        var last = AddTask("Last");

        _board.Delete(middle.Id);
        Assert.True(_board.UndoDelete());

        Assert.Equal([first, middle, last], Today);
        Assert.False(_board.CanUndoDelete);
    }

    [Fact]
    public void UndoDelete_OnlyRestoresMostRecentDelete()
    {
        var a = AddTask("A");
        var b = AddTask("B");

        _board.Delete(a.Id);
        _board.Delete(b.Id);

        Assert.True(_board.UndoDelete());
        Assert.False(_board.UndoDelete());
        Assert.Equal([b], _board.Document.Tasks);
    }

    [Fact]
    public void UndoDelete_WithNothingDeleted_DoesNothing()
    {
        Assert.False(_board.CanUndoDelete);
        Assert.False(_board.UndoDelete());
        Assert.Equal(0, _changedCount);
    }

    [Fact]
    public void ClearUndo_ForgetsDeletedTask()
    {
        var task = AddTask();
        _board.Delete(task.Id);

        _board.ClearUndo();

        Assert.False(_board.CanUndoDelete);
        Assert.False(_board.UndoDelete());
    }

    [Fact]
    public void Delete_UnknownId_DoesNothing()
    {
        Assert.False(_board.Delete(Guid.NewGuid()));
        Assert.False(_board.CanUndoDelete);
        Assert.Equal(0, _changedCount);
    }

    // --- Move ----------------------------------------------------------------

    [Fact]
    public void MoveToInbox_ClearsPlannedDate()
    {
        var task = AddTask();

        Assert.True(_board.MoveToInbox(task.Id));

        Assert.Null(Get(task.Id).PlannedFor);
        Assert.Empty(Today);
        Assert.Single(Inbox);
    }

    [Fact]
    public void MoveToToday_PlansForToday_AtBottom()
    {
        var moved = AddTask("Moved");
        var other = AddTask("Other");
        _board.MoveToInbox(moved.Id);

        Assert.True(_board.MoveToToday(moved.Id));

        Assert.Equal(TestClock.Day16, Get(moved.Id).PlannedFor);
        Assert.Equal(["Other", "Moved"], Today.Select(task => task.Title));
        Assert.Equal(other.Id, Today[0].Id);
    }

    [Fact]
    public void MoveToInbox_PutsTaskAtBottomOfInbox()
    {
        var a = AddTask("A");
        var b = AddTask("B");

        _board.MoveToInbox(b.Id);
        _board.MoveToInbox(a.Id);

        Assert.Equal(["B", "A"], Inbox.Select(task => task.Title));
    }

    [Fact]
    public void Move_ToListItIsAlreadyIn_DoesNothing()
    {
        var task = AddTask();

        Assert.False(_board.MoveToToday(task.Id));
        _board.MoveToInbox(task.Id);
        Assert.False(_board.MoveToInbox(task.Id));
        Assert.Equal(2, _changedCount);
    }

    [Fact]
    public void MoveToToday_RolledOverTask_KeepsItsDate()
    {
        // Already shown in Today (rolled over), so it isn't "moved" again.
        var task = AddTask();
        _time.Advance(TimeSpan.FromDays(2));

        Assert.False(_board.MoveToToday(task.Id));
        Assert.Equal(TestClock.Day16, Get(task.Id).PlannedFor);
    }

    [Fact]
    public void Move_UnknownId_DoesNothing()
    {
        Assert.False(_board.MoveToInbox(Guid.NewGuid()));
        Assert.False(_board.MoveToToday(Guid.NewGuid()));
        Assert.Equal(0, _changedCount);
    }

    // --- Rename --------------------------------------------------------------

    [Fact]
    public void Rename_NormalizesTitle()
    {
        var task = AddTask("Old");

        Assert.True(_board.Rename(task.Id, "  New  "));

        Assert.Equal("New", Get(task.Id).Title);
        Assert.Equal(2, _changedCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Rename_ToEmpty_KeepsOldTitle(string? title)
    {
        var task = AddTask("Old");

        Assert.False(_board.Rename(task.Id, title));
        Assert.Equal("Old", Get(task.Id).Title);
    }

    [Fact]
    public void Rename_ToSameTitle_DoesNothing()
    {
        var task = AddTask("Same");

        Assert.False(_board.Rename(task.Id, " Same "));
        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public void Rename_UnknownId_DoesNothing()
    {
        Assert.False(_board.Rename(Guid.NewGuid(), "New"));
    }
}
