using Ledge.Core.Abstractions;
using Ledge.Core.Models;

namespace Ledge.Core.Services;

/// <summary>
/// Stores tasks in "tasks.json" inside a folder (the app passes
/// %LOCALAPPDATA%\Ledge; tests pass a temp folder). See ARCHITECTURE.md §5.
/// Saving, loading and recovery are done by <see cref="JsonFile{T}"/>.
/// </summary>
public sealed class JsonTaskStore : ITaskStore
{
    public const string FileName = "tasks.json";

    private readonly JsonFile<TaskDocument> _file;

    public JsonTaskStore(string folder, TimeProvider time)
    {
        _file = new JsonFile<TaskDocument>(Path.Combine(folder, FileName), time, IsUsable);
    }

    public string FilePath => _file.FilePath;

    public async Task<TaskLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        var (document, outcome) = await _file.LoadAsync(cancellationToken).ConfigureAwait(false);

        return new TaskLoadResult(document ?? new TaskDocument(), outcome switch
        {
            JsonFileLoadOutcome.Loaded => TaskLoadOutcome.Loaded,
            JsonFileLoadOutcome.RestoredFromBackup => TaskLoadOutcome.RestoredFromBackup,
            JsonFileLoadOutcome.ResetAfterCorruption => TaskLoadOutcome.ResetAfterCorruption,
            _ => TaskLoadOutcome.FirstRun,
        });
    }

    public Task SaveAsync(TaskDocument document, CancellationToken cancellationToken = default) =>
        _file.SaveAsync(document, cancellationToken);

    /// <summary>
    /// The JSON parsed, but is it something we can use? A file from a newer
    /// Ledge might hold data this build doesn't understand, so we don't load
    /// (and later overwrite) it. Only structure is checked: one odd title
    /// shouldn't throw away the whole file.
    /// </summary>
    private static bool IsUsable(TaskDocument document) =>
        document is { Tasks: not null } &&
        document.SchemaVersion is >= 1 and <= TaskDocument.CurrentSchemaVersion &&
        document.Tasks.All(task => task is not null);
}
