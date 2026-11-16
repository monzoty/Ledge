using System.Globalization;
using System.Text.Json;
using Ledge.Core.Abstractions;
using Ledge.Core.Models;

namespace Ledge.Core.Services;

/// <summary>
/// Stores tasks in "tasks.json" inside a folder (the app passes
/// %LOCALAPPDATA%\Ledge; tests pass a temp folder). See ARCHITECTURE.md §5.
/// </summary>
/// <remarks>
/// Load order: tasks.json → tasks.json.bak → empty. A file that can't be used
/// (broken JSON, missing fields, or written by a NEWER Ledge) is never deleted:
/// it is renamed to "tasks.corrupt-&lt;time&gt;.json" so nothing is lost, and so
/// the next save can't overwrite it.
/// </remarks>
public sealed class JsonTaskStore : ITaskStore
{
    public const string FileName = "tasks.json";

    private const string CorruptStampFormat = "yyyyMMdd-HHmmss";

    // Web defaults = camelCase names ("plannedFor"). DateOnly is written as
    // "2026-11-16", DateTimeOffset as ISO 8601. Indented so you can read the file.
    // https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/overview
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly TimeProvider _time;

    // Only one save at a time: two overlapping saves would fight over tasks.json.tmp.
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public JsonTaskStore(string folder, TimeProvider time)
    {
        FilePath = Path.Combine(folder, FileName);
        _time = time;
    }

    public string FilePath { get; }

    public async Task<TaskLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        var main = await TryReadAsync(FilePath, cancellationToken).ConfigureAwait(false);
        if (main.Document is { } document)
        {
            return new TaskLoadResult(document, TaskLoadOutcome.Loaded);
        }

        if (main.IsCorrupt)
        {
            SetAside(FilePath, "corrupt");
        }

        var backupPath = AtomicFile.BackupPath(FilePath);
        var backup = await TryReadAsync(backupPath, cancellationToken).ConfigureAwait(false);
        if (backup.Document is { } restored)
        {
            return new TaskLoadResult(restored, TaskLoadOutcome.RestoredFromBackup);
        }

        if (backup.IsCorrupt)
        {
            SetAside(backupPath, "bak.corrupt");
        }

        var outcome = main.IsCorrupt || backup.IsCorrupt
            ? TaskLoadOutcome.ResetAfterCorruption
            : TaskLoadOutcome.FirstRun;

        return new TaskLoadResult(new TaskDocument(), outcome);
    }

    public async Task SaveAsync(TaskDocument document, CancellationToken cancellationToken = default)
    {
        await _saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await AtomicFile.WriteAsync(
                FilePath,
                (stream, token) => JsonSerializer.SerializeAsync(stream, document, JsonOptions, token),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    /// <summary>
    /// Reads one file. Missing → (null, not corrupt). Unusable content →
    /// (null, corrupt). Other I/O errors (locked, access denied) are thrown:
    /// those aren't the file's fault, so it must not be renamed.
    /// </summary>
    private static async Task<ReadAttempt> TryReadAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return ReadAttempt.Missing;
        }

        try
        {
            var stream = File.OpenRead(path);
            await using (stream.ConfigureAwait(false))
            {
                var document = await JsonSerializer
                    .DeserializeAsync<TaskDocument>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);

                return IsUsable(document) ? new ReadAttempt(document, IsCorrupt: false) : ReadAttempt.Corrupt;
            }
        }
        catch (JsonException)
        {
            // Broken JSON, wrong types, or a `required` property missing.
            return ReadAttempt.Corrupt;
        }
    }

    /// <summary>
    /// The JSON parsed, but is it something we can use? A file from a newer
    /// Ledge might hold data this build doesn't understand, so we don't load
    /// (and later overwrite) it.
    /// </summary>
    private static bool IsUsable(TaskDocument? document) =>
        document is { Tasks: not null } &&
        document.SchemaVersion is >= 1 and <= TaskDocument.CurrentSchemaVersion &&
        document.Tasks.All(task => task is not null);

    /// <summary>Renames an unusable file out of the way, e.g. tasks.corrupt-20261116-101500.json.</summary>
    private void SetAside(string path, string label)
    {
        var folder = Path.GetDirectoryName(path)!;
        var stamp = _time.GetLocalNow().ToString(CorruptStampFormat, CultureInfo.InvariantCulture);
        var baseName = $"{Path.GetFileNameWithoutExtension(FileName)}.{label}-{stamp}";

        // Same second twice (unlikely)? Add -2, -3, … rather than overwrite.
        var target = Path.Combine(folder, baseName + ".json");
        for (var n = 2; File.Exists(target); n++)
        {
            target = Path.Combine(folder, $"{baseName}-{n}.json");
        }

        File.Move(path, target);
    }

    private sealed record ReadAttempt(TaskDocument? Document, bool IsCorrupt)
    {
        public static readonly ReadAttempt Missing = new(null, IsCorrupt: false);
        public static readonly ReadAttempt Corrupt = new(null, IsCorrupt: true);
    }
}
