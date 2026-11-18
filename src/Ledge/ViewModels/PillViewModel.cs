using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ledge.ViewModels;

/// <summary>
/// The collapsed pill (PRD F1.4): decides which of its three looks to show.
/// The timer wins over the task count; the count wins over "All clear".
/// The pill's XAML binds to <see cref="Mode"/> for the cross-fade, and reads
/// the numbers through <see cref="Tasks"/> and <see cref="Timer"/>.
/// </summary>
public sealed partial class PillViewModel : ObservableObject
{
    [ObservableProperty]
    private PillMode _mode;

    public PillViewModel(TaskListViewModel tasks, TimerViewModel timer)
    {
        Tasks = tasks;
        Timer = timer;

        tasks.PropertyChanged += OnSourceChanged;
        timer.PropertyChanged += OnSourceChanged;
        UpdateMode();
    }

    public TaskListViewModel Tasks { get; }

    public TimerViewModel Timer { get; }

    private void OnSourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TaskListViewModel.HasOpenToday) or nameof(TimerViewModel.State))
        {
            UpdateMode();
        }
    }

    private void UpdateMode() => Mode =
        !Timer.IsIdle ? PillMode.Timer
        : Tasks.HasOpenToday ? PillMode.TodayCount
        : PillMode.AllClear;
}
