using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ledge.Core.Models;
using Ledge.Core.Services;

namespace Ledge.ViewModels;

/// <summary>
/// What the panel's task area shows and can do: quick-add, the Today list and
/// the collapsible Inbox. Delete/undo, rename and keyboard actions come in step 3d.
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

    [ObservableProperty]
    private string _quickAddText = string.Empty;

    /// <summary>Open tasks in Today: the number in "TODAY · n" (DESIGN.md §2).</summary>
    [ObservableProperty]
    private int _todayOpenCount;

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

    public TaskListViewModel(TaskBoard board, TimeProvider time)
    {
        _board = board;
        _time = time;

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
    }

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

    /// <summary>Checkbox click (PRD F3.3). The board raises Changed, which refreshes the row.</summary>
    [RelayCommand]
    private void Toggle(TaskRowViewModel? row)
    {
        if (row is not null)
        {
            _board.Toggle(row.Id);
        }
    }

    /// <summary>
    /// The row's move icon (PRD F3.5): Today → Inbox, Inbox → Today. The row
    /// leaves one list and appears at the bottom of the other.
    /// </summary>
    [RelayCommand]
    private void Move(TaskRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        if (row.IsInInbox)
        {
            _board.MoveToToday(row.Id);
        }
        else
        {
            _board.MoveToInbox(row.Id);
        }
    }

    /// <summary>Click on the "INBOX · n" header.</summary>
    [RelayCommand]
    private void ToggleInbox() => IsInboxExpanded = !IsInboxExpanded;

    /// <summary>
    /// Makes <paramref name="rows"/> match <paramref name="tasks"/> (same order)
    /// with the fewest changes: existing rows are updated and moved, new ones
    /// inserted, and leftovers removed from the end.
    /// </summary>
    private static void Sync(ObservableCollection<TaskRowViewModel> rows, IReadOnlyList<TaskItem> tasks)
    {
        for (var i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];
            var existing = IndexOf(rows, task.Id, startAt: i);

            if (existing < 0)
            {
                rows.Insert(i, new TaskRowViewModel(task));
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
