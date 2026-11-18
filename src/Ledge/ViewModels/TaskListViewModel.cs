using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ledge.Core.Models;
using Ledge.Core.Services;

namespace Ledge.ViewModels;

/// <summary>
/// What the panel's task area shows and can do: quick-add, the Today list,
/// the collapsible Inbox, delete with undo, and rename.
/// </summary>
/// <remarks>
/// MVVM: the View (XAML) binds to properties and commands here, and this
/// class has no idea what the View looks like. That keeps logic testable
/// without any window.
/// <list type="bullet">
/// <item><c>[ObservableProperty]</c> on <c>_quickAddText</c> generates a public
/// <c>QuickAddText</c> property that tells bindings when it changes.</item>
/// <item><c>[RelayCommand]</c> on <c>Add()</c> generates an <c>AddCommand</c>
/// (an ICommand) that XAML can call, e.g. from a key binding.</item>
/// </list>
/// https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/generators/overview
/// Generated code lives in obj/…/generated; you can open it in the IDE
/// (Dependencies → Analyzers → CommunityToolkit.Mvvm.SourceGenerators).
/// </remarks>
public sealed partial class TaskListViewModel : ObservableObject
{
    private readonly TaskBoard _board;
    private readonly TimeProvider _time;
    private readonly TaskListMotion _motion;
    private readonly TimerViewModel _timer;

    [ObservableProperty]
    private string _quickAddText = string.Empty;

    /// <summary>Open tasks in Today: the number in "TODAY · n" and in the pill's "n today" (DESIGN.md §2).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOpenToday))]
    private int _todayOpenCount;

    /// <summary>
    /// The task file couldn't be read and the backup was loaded (DESIGN.md §6).
    /// Shown until the panel closes for the first time.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDataNotice))]
    private bool _showRestoredNotice;

    /// <summary>Neither the file nor the backup could be read; Ledge started empty.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDataNotice))]
    private bool _showResetNotice;

    /// <summary>True when Today has no rows at all; shows the empty-state text.</summary>
    [ObservableProperty]
    private bool _isTodayEmpty = true;

    /// <summary>Open tasks in the Inbox: the number in "INBOX · n".</summary>
    [ObservableProperty]
    private int _inboxOpenCount;

    /// <summary>
    /// Inbox open or closed. Collapsed by default; kept while the app runs
    /// (this ViewModel is a singleton) but not saved (DESIGN.md §2).
    /// [NotifyPropertyChangedFor] also announces ShowInboxEmpty, which depends on it.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInboxEmpty))]
    private bool _isInboxExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInboxEmpty))]
    private bool _isInboxEmpty = true;

    /// <summary>"Task deleted · Ctrl+Z to undo" is visible (DESIGN.md §2 "Undo hint").</summary>
    [ObservableProperty]
    private bool _showUndoHint;

    /// <summary>
    /// "Focus session done · Mark task done" at the bottom of the panel, after
    /// a linked session ends (DESIGN.md §2 "When a focus session ends").
    /// </summary>
    [ObservableProperty]
    private bool _showFocusDoneLine;

    /// <summary>The task that line is about.</summary>
    private Guid? _focusDoneTaskId;

    /// <summary>
    /// Some row's rename box is open. The window checks this so Esc cancels
    /// the rename instead of closing the panel (DESIGN.md §5).
    /// </summary>
    [ObservableProperty]
    private bool _isRenaming;

    public TaskListViewModel(
        TaskBoard board,
        TimeProvider time,
        TaskListMotion motion,
        TaskLoadResult loaded,
        TimerViewModel timer)
    {
        _board = board;
        _time = time;
        _motion = motion;
        _timer = timer;

        // Keep the amber stopwatch on the right row when a session starts or ends.
        _timer.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TimerViewModel.LinkedTaskId))
            {
                MarkLinkedRow();
            }
        };

        ShowRestoredNotice = loaded.Outcome == TaskLoadOutcome.RestoredFromBackup;
        ShowResetNotice = loaded.Outcome == TaskLoadOutcome.ResetAfterCorruption;

        // Both live for the whole app (singletons), so this subscription never leaks.
        _board.Changed += (_, _) => Refresh();
        Refresh();
    }

    /// <summary>
    /// The rows of the Today list. An ObservableCollection tells the ItemsControl
    /// about each insert/move/remove, so only the affected rows are redrawn.
    /// https://learn.microsoft.com/dotnet/api/system.collections.objectmodel.observablecollection-1
    /// </summary>
    public ObservableCollection<TaskRowViewModel> Today { get; } = [];

    /// <summary>The rows of the Inbox (tasks without a planned date).</summary>
    public ObservableCollection<TaskRowViewModel> Inbox { get; } = [];

    /// <summary>"Inbox is clear." shows only when the Inbox is open AND empty.</summary>
    public bool ShowInboxEmpty => IsInboxExpanded && IsInboxEmpty;

    /// <summary>The pill shows "n today" with the amber dot; otherwise "All clear" (DESIGN.md §2).</summary>
    public bool HasOpenToday => TodayOpenCount > 0;

    /// <summary>Either data notice is showing (the line at the bottom of the panel).</summary>
    public bool ShowDataNotice => ShowRestoredNotice || ShowResetNotice;

    /// <summary>
    /// Re-reads the lists from the board. Rows are matched by task Id and
    /// updated in place rather than recreated (see <see cref="Sync"/>).
    /// Also called at midnight and on wake-up, when "today" itself changes.
    /// </summary>
    public void Refresh()
    {
        var today = TaskQueries.Today(_board.Document, _time);
        Sync(Today, today);
        TodayOpenCount = today.Count(task => !task.IsDone);
        IsTodayEmpty = Today.Count == 0;

        var inbox = TaskQueries.Inbox(_board.Document, _time);
        Sync(Inbox, inbox);
        InboxOpenCount = inbox.Count(task => !task.IsDone);
        IsInboxEmpty = Inbox.Count == 0;

        MarkLinkedRow();
    }

    /// <summary>
    /// The panel closed: forget the undo slot and hide the hint (PRD F3.4:
    /// "undoes the most recent delete while the panel is open").
    /// </summary>
    public void ClearUndo()
    {
        _board.ClearUndo();
        ShowUndoHint = false;
    }

    /// <summary>
    /// The panel closed: undo ends, and a data notice has now been seen once
    /// (DESIGN.md §2 "Data notice").
    /// </summary>
    public void OnPanelClosed()
    {
        ClearUndo();
        ShowRestoredNotice = false;
        ShowResetNotice = false;
        HideFocusDoneLine();
    }

    /// <summary>
    /// A focus session ended. If it was for a task that's still open, offer
    /// "Mark task done" in the panel (PRD F4.6). Returns that task's title for
    /// the notification, or null if there's no such task.
    /// </summary>
    public string? OnFocusFinished(Guid? taskId)
    {
        var task = _board.Document.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null)
        {
            return null; // unlinked, or the task was deleted meanwhile
        }

        if (!task.IsDone)
        {
            _focusDoneTaskId = task.Id;
            ShowFocusDoneLine = true;
        }

        return task.Title;
    }

    /// <summary>The "Mark task done" chip. Only ever marks done, never un-does.</summary>
    [RelayCommand]
    private void MarkFocusTaskDone()
    {
        if (_focusDoneTaskId is { } id && _board.Document.Tasks.FirstOrDefault(t => t.Id == id) is { IsDone: false })
        {
            _board.Toggle(id);
        }

        HideFocusDoneLine();
    }

    private void HideFocusDoneLine()
    {
        ShowFocusDoneLine = false;
        _focusDoneTaskId = null;
    }

    // --- Called by TaskRowViewModel ------------------------------------------

    internal void Toggle(TaskRowViewModel row) => _board.Toggle(row.Id);

    /// <summary>Today → Inbox, Inbox → Today. The row leaves one list and appears at the bottom of the other.</summary>
    internal void Move(TaskRowViewModel row)
    {
        if (row.IsInInbox)
        {
            _board.MoveToToday(row.Id);
        }
        else
        {
            _board.MoveToInbox(row.Id);
        }
    }

    /// <summary>
    /// Plays the remove animation (the row's IsRemoving starts it in XAML), waits
    /// for it, then deletes. Waiting with the injected TimeProvider keeps it testable.
    /// </summary>
    internal async Task DeleteAsync(TaskRowViewModel row)
    {
        row.IsRemoving = true;
        await Task.Delay(_motion.RowRemoveDuration, _time);

        if (_board.Delete(row.Id))
        {
            ShowUndoHint = true;
        }
    }

    internal void Rename(TaskRowViewModel row, string newTitle) => _board.Rename(row.Id, newTitle);

    /// <summary>Ctrl+Enter on a row; the timer asks first if a session is already going.</summary>
    internal void StartFocus(TaskRowViewModel row) => _timer.RequestStart(row.Id);

    /// <summary>Exactly one row (or none) shows the stopwatch: the one the timer is for.</summary>
    private void MarkLinkedRow()
    {
        var linked = _timer.LinkedTaskId;
        foreach (var row in Today.Concat(Inbox))
        {
            row.IsLinkedToTimer = row.Id == linked;
        }
    }

    internal void OnEditingChanged() => IsRenaming = Today.Concat(Inbox).Any(row => row.IsEditing);

    // --- Commands ----------------------------------------------------------------

    /// <summary>
    /// Enter in quick-add (PRD F3.1): add to Today, then clear the box so the
    /// next task can be typed straight away. Empty input does nothing (F3.2).
    /// </summary>
    [RelayCommand]
    private void Add()
    {
        if (_board.Add(QuickAddText) is null)
        {
            return;
        }

        QuickAddText = string.Empty;
    }

    /// <summary>Ctrl+Z: bring back the most recently deleted task (PRD F3.4).</summary>
    [RelayCommand]
    private void UndoDelete()
    {
        _board.UndoDelete();
        ShowUndoHint = false;
    }

    /// <summary>Click on the "INBOX · n" header.</summary>
    [RelayCommand]
    private void ToggleInbox() => IsInboxExpanded = !IsInboxExpanded;

    // --- Keeping the rows in step with the board --------------------------------

    /// <summary>
    /// Makes <paramref name="rows"/> match <paramref name="tasks"/> (same order)
    /// with the fewest changes: existing rows are updated and moved, new ones
    /// inserted, and leftovers removed from the end.
    /// </summary>
    private void Sync(ObservableCollection<TaskRowViewModel> rows, IReadOnlyList<TaskItem> tasks)
    {
        for (var i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];
            var existing = IndexOf(rows, task.Id, startAt: i);

            if (existing < 0)
            {
                rows.Insert(i, new TaskRowViewModel(task, this));
                continue;
            }

            if (existing != i)
            {
                rows.Move(existing, i);
            }

            rows[i].Update(task);
        }

        while (rows.Count > tasks.Count)
        {
            rows.RemoveAt(rows.Count - 1);
        }
    }

    /// <summary>Rows before <paramref name="startAt"/> are already in place, so only search after them.</summary>
    private static int IndexOf(ObservableCollection<TaskRowViewModel> rows, Guid id, int startAt)
    {
        for (var i = startAt; i < rows.Count; i++)
        {
            if (rows[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }
}
