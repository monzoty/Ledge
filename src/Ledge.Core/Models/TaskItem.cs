namespace Ledge.Core.Models;

/// <summary>
/// One task. Immutable: to change a task, make a copy with <c>with { … }</c>
/// (see TaskBoard). That makes undo and change detection simple.
/// </summary>
public sealed record TaskItem
{
    public required Guid Id { get; init; }

    /// <summary>Already normalised: trimmed, 1–200 characters (see <see cref="Services.TaskTitle"/>).</summary>
    public required string Title { get; init; }

    /// <summary>The local day the task is planned for; null means it lives in the Inbox.</summary>
    public DateOnly? PlannedFor { get; init; }

    public bool IsDone { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Set when the task is completed; cleared when it is un-completed.</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Position within its list; lower comes first.</summary>
    public int SortOrder { get; init; }
}
