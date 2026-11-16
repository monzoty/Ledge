using Ledge.Core.Models;

namespace Ledge.Core.Abstractions;

/// <summary>
/// Loads and saves the task document. The app uses JsonTaskStore; tests of
/// other classes can use a fake, so they never touch the disk.
/// </summary>
public interface ITaskStore
{
    /// <summary>
    /// Loads the saved tasks. Never throws for a broken file: it falls back to
    /// the backup or an empty document, and the result says which happened.
    /// I/O errors such as "access denied" are still thrown.
    /// </summary>
    Task<TaskLoadResult> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves atomically: after a crash you have the old file or the new one, never half of one.</summary>
    Task SaveAsync(TaskDocument document, CancellationToken cancellationToken = default);
}
