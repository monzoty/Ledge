using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ledge.Core.Models;

namespace Ledge.ViewModels;

/// <summary>
/// One row in a task list. It lives as long as the task is in the list and is
/// UPDATED in place when the task changes, so the row on screen stays the same
/// object and can animate (e.g. the "done" transition, DESIGN.md §4).
/// </summary>
/// <remarks>
/// Each row has its own commands (Toggle, Delete, Rename…), so the row's XAML
/// and its keyboard shortcuts bind straight to them. The real work is done by
/// the owning <see cref="TaskListViewModel"/>.
/// While renaming or being removed, the row's other commands are disabled
/// (CanExecute = false), so e.g. typing a space in the rename box doesn't
/// also tick the task.
/// </remarks>
public sealed partial class TaskRowViewModel : ObservableObject
{
    private readonly TaskListViewModel _owner;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _isDone;

    /// <summary>No planned date: the row offers "Move to Today" instead of "Move to Inbox".</summary>
    [ObservableProperty]
    private bool _isInInbox;

    /// <summary>Inline rename is open (PRD F3.6).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleCommand), nameof(MoveCommand), nameof(DeleteCommand), nameof(BeginRenameCommand), nameof(StartFocusCommand))]
    private bool _isEditing;

    /// <summary>The text in the rename box; becomes the title on commit.</summary>
    [ObservableProperty]
    private string _editText = string.Empty;

    /// <summary>The focus timer is running (or paused) for this task: shows the amber stopwatch.</summary>
    [ObservableProperty]
    private bool _isLinkedToTimer;

    /// <summary>The delete animation is playing (DESIGN.md §4); the task is deleted when it ends.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleCommand), nameof(MoveCommand), nameof(DeleteCommand), nameof(BeginRenameCommand), nameof(StartFocusCommand))]
    private bool _isRemoving;

    public TaskRowViewModel(TaskItem task, TaskListViewModel owner)
    {
        Id = task.Id;
        _owner = owner;
        Update(task);
    }

    public Guid Id { get; }

    /// <summary>Copies the latest values; bindings only refresh what actually changed.</summary>
    public void Update(TaskItem task)
    {
        Title = task.Title;
        IsDone = task.IsDone;
        IsInInbox = task.PlannedFor is null;
    }

    private bool IsIdle => !IsEditing && !IsRemoving;

    /// <summary>Checkbox or Space (PRD F3.3).</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void Toggle() => _owner.Toggle(this);

    /// <summary>Move icon or Ctrl+I (PRD F3.5).</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void Move() => _owner.Move(this);

    /// <summary>Trash icon or Del (PRD F3.4). Plays the remove animation first.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private Task DeleteAsync() => _owner.DeleteAsync(this);

    /// <summary>Ctrl+Enter (DESIGN.md §5): a focus session for this task, with the selected preset.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void StartFocus() => _owner.StartFocus(this);

    /// <summary>F2 or double-click: open the rename box with the current title.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void BeginRename()
    {
        EditText = Title;
        IsEditing = true;
        _owner.OnEditingChanged();
    }

    /// <summary>
    /// Enter, or the box losing focus (DESIGN.md §2: blur saves). Safe to call
    /// twice: after Enter closes the box, the focus loss that follows does nothing.
    /// </summary>
    [RelayCommand]
    private void CommitRename()
    {
        if (!IsEditing)
        {
            return;
        }

        IsEditing = false;
        _owner.Rename(this, EditText); // an empty title keeps the old one (TaskBoard.Rename)
        _owner.OnEditingChanged();
    }

    /// <summary>Esc while renaming: close the box, keep the old title.</summary>
    [RelayCommand]
    private void CancelRename()
    {
        IsEditing = false;
        _owner.OnEditingChanged();
    }
}
