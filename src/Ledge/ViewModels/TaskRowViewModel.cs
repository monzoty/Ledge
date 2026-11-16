using CommunityToolkit.Mvvm.ComponentModel;
using Ledge.Core.Models;

namespace Ledge.ViewModels;

/// <summary>
/// One row in a task list. It lives as long as the task is in the list and is
/// UPDATED in place when the task changes, so the row on screen stays the same
/// object and can animate (e.g. the "done" transition, DESIGN.md §4).
/// </summary>
public sealed partial class TaskRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _isDone;

    /// <summary>No planned date: the row offers "Move to Today" instead of "Move to Inbox".</summary>
    [ObservableProperty]
    private bool _isInInbox;

    public TaskRowViewModel(TaskItem task)
    {
        Id = task.Id;
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
}
