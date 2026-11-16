using Ledge.Core.Models;

namespace Ledge.Core.Services;

/// <summary>
/// Turns a document plus "now" into the Today and Inbox lists (PRD F3,
/// ARCHITECTURE.md §4). Pure functions: nothing is stored, so midnight
/// rollover needs no data changes — the same document simply gives a
/// different answer on a new day.
/// </summary>
/// <remarks>
/// A done task stays in the list it was in (Today if it has a planned date,
/// Inbox if not), struck through, until the local day changes.
/// </remarks>
public static class TaskQueries
{
    /// <summary>The user's current local calendar date.</summary>
    public static DateOnly CurrentDate(TimeProvider time) =>
        DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    /// <summary>
    /// Not done and planned for today or earlier (earlier = rolled over),
    /// plus planned tasks completed today.
    /// </summary>
    public static IReadOnlyList<TaskItem> Today(TaskDocument document, TimeProvider time)
    {
        var today = CurrentDate(time);
        return Sorted(document.Tasks.Where(task =>
            task.PlannedFor is { } planned &&
            (task.IsDone ? CompletedOn(task, time) == today : planned <= today)));
    }

    /// <summary>Tasks without a planned date: not done, or completed today.</summary>
    public static IReadOnlyList<TaskItem> Inbox(TaskDocument document, TimeProvider time)
    {
        var today = CurrentDate(time);
        return Sorted(document.Tasks.Where(task =>
            task.PlannedFor is null &&
            (!task.IsDone || CompletedOn(task, time) == today)));
    }

    /// <summary>
    /// The LOCAL date a task was completed on. CompletedAt is an exact instant;
    /// 23:30 in India is still the previous day in UTC, so convert first.
    /// </summary>
    private static DateOnly? CompletedOn(TaskItem task, TimeProvider time)
    {
        if (task.CompletedAt is not { } completedAt)
        {
            return null;
        }

        var local = TimeZoneInfo.ConvertTime(completedAt, time.LocalTimeZone);
        return DateOnly.FromDateTime(local.DateTime);
    }

    private static List<TaskItem> Sorted(IEnumerable<TaskItem> tasks) =>
        [.. tasks.OrderBy(task => task.SortOrder).ThenBy(task => task.CreatedAt)];
}
