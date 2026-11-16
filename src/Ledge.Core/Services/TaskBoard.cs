using Ledge.Core.Models;

namespace Ledge.Core.Services;

/// <summary>
/// The single in-memory owner of the task document (ARCHITECTURE.md §3).
/// Every change replaces <see cref="Document"/> with a new immutable copy and
/// raises <see cref="Changed"/>. ViewModels listen to Changed and rebuild
/// their lists; from step 2 the board also asks ITaskStore to save.
/// </summary>
/// <remarks>
/// Each operation returns true if something changed and false otherwise
/// (unknown id, empty title, already in that state). Changed is only raised
/// when something actually changed, so nothing redraws or saves for nothing.
/// </remarks>
public sealed class TaskBoard
{
    private readonly TimeProvider _time;

    /// <summary>The most recently deleted task, for Ctrl+Z (PRD F3.4); one level only.</summary>
    private TaskItem? _lastDeleted;

    public TaskBoard(TimeProvider time, TaskDocument? document = null)
    {
        _time = time;
        Document = document ?? new TaskDocument();
    }

    public event EventHandler? Changed;

    public TaskDocument Document { get; private set; }

    public bool CanUndoDelete => _lastDeleted is not null;

    /// <summary>
    /// Quick-add (PRD F3.1): a new task planned for today, at the bottom of Today.
    /// Returns the new task, or null if the title was empty.
    /// </summary>
    public TaskItem? Add(string? title)
    {
        var normalized = TaskTitle.Normalize(title);
        if (normalized is null)
        {
            return null;
        }

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = normalized,
            PlannedFor = TaskQueries.CurrentDate(_time),
            CreatedAt = _time.GetUtcNow(),
            SortOrder = NextSortOrder(),
        };

        Commit([.. Document.Tasks, task]);
        return task;
    }

    /// <summary>Done ⇄ not done (PRD F3.3). Records when it was completed.</summary>
    public bool Toggle(Guid id) =>
        Update(id, task => task.IsDone
            ? task with { IsDone = false, CompletedAt = null }
            : task with { IsDone = true, CompletedAt = _time.GetUtcNow() });

    /// <summary>Removes a task; it can be brought back with <see cref="UndoDelete"/>.</summary>
    public bool Delete(Guid id)
    {
        var task = Find(id);
        if (task is null)
        {
            return false;
        }

        _lastDeleted = task;
        Commit([.. Document.Tasks.Where(t => t.Id != id)]);
        return true;
    }

    /// <summary>
    /// Restores the most recently deleted task, exactly as it was (same id and
    /// SortOrder, so it returns to the same position).
    /// </summary>
    public bool UndoDelete()
    {
        if (_lastDeleted is not { } task)
        {
            return false;
        }

        _lastDeleted = null;
        Commit([.. Document.Tasks, task]);
        return true;
    }

    /// <summary>Forgets the undo slot (the panel calls this when it closes).</summary>
    public void ClearUndo() => _lastDeleted = null;

    /// <summary>Takes away the planned date; the task moves to the bottom of the Inbox (PRD F3.5).</summary>
    public bool MoveToInbox(Guid id) =>
        Update(id, task => task.PlannedFor is null
            ? task
            : task with { PlannedFor = null, SortOrder = NextSortOrder() });

    /// <summary>Plans an Inbox task for today; it moves to the bottom of Today (PRD F3.5).</summary>
    public bool MoveToToday(Guid id) =>
        Update(id, task => task.PlannedFor is not null
            ? task
            : task with { PlannedFor = TaskQueries.CurrentDate(_time), SortOrder = NextSortOrder() });

    /// <summary>Renames a task (PRD F3.6). An empty new title is ignored.</summary>
    public bool Rename(Guid id, string? title)
    {
        var normalized = TaskTitle.Normalize(title);
        return normalized is not null && Update(id, task => task with { Title = normalized });
    }

    private TaskItem? Find(Guid id) => Document.Tasks.FirstOrDefault(task => task.Id == id);

    /// <summary>
    /// Applies <paramref name="change"/> to one task. Records compare by value,
    /// so "nothing changed" is simply <c>updated == task</c>.
    /// </summary>
    private bool Update(Guid id, Func<TaskItem, TaskItem> change)
    {
        var task = Find(id);
        if (task is null)
        {
            return false;
        }

        var updated = change(task);
        if (updated == task)
        {
            return false;
        }

        Commit([.. Document.Tasks.Select(t => t.Id == id ? updated : t)]);
        return true;
    }

    /// <summary>One higher than any task's SortOrder, so the task lands at the bottom of whichever list it's in.</summary>
    private int NextSortOrder() =>
        Document.Tasks.Count == 0 ? 0 : Document.Tasks.Max(task => task.SortOrder) + 1;

    private void Commit(IReadOnlyList<TaskItem> tasks)
    {
        Document = Document with { Tasks = tasks };
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
