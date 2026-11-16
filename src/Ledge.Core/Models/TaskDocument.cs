namespace Ledge.Core.Models;

/// <summary>
/// Everything that gets saved to tasks.json (ARCHITECTURE.md §4–§5).
/// </summary>
public sealed record TaskDocument
{
    /// <summary>The file format version written by this build.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Lets a future version recognise and migrate older files.</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public IReadOnlyList<TaskItem> Tasks { get; init; } = [];
}
