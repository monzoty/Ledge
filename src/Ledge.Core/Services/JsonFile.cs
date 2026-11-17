using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ledge.Core.Services;

/// <summary>
/// One JSON document on disk, saved crash-safely and loaded with recovery
/// (ARCHITECTURE.md §5). Shared by JsonTaskStore (tasks.json) and
/// JsonSettingsStore (settings.json), so both behave exactly the same.
/// </summary>
/// <typeparam name="T">
/// The document type. "Generic": the class is written once and works for any
/// <c>T</c>; the compiler makes a typed version for each one used.
/// https://learn.microsoft.com/dotnet/csharp/fundamentals/types/generics
/// </typeparam>
/// <remarks>
/// Load order: file → file.bak → nothing. A file that can't be used (broken
/// JSON, missing required fields, or rejected by <c>isUsable</c>, e.g. written
/// by a NEWER Ledge) is never deleted: it's renamed to
/// "&lt;name&gt;.corrupt-&lt;time&gt;.json" so nothing is lost and the next
/// save can't overwrite it. I/O errors such as "access denied" are thrown:
/// those aren't the file's fault, so it must not be renamed.
/// </remarks>
internal sealed class JsonFile<T>
    where T : class
{
    private const string CorruptStampFormat = "yyyyMMdd-HHmmss";

    // Web defaults = camelCase names ("plannedFor"). DateOnly is written as
    // "2026-11-16", DateTimeOffset as ISO 8601. Enums as camelCase text
    // ("theme": "dark") instead of numbers. Indented so you can read the file.
    // https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/overview
    //
    // The default encoder escapes "+" as + and every non-ASCII letter or
    // emoji, which only matters when JSON is pasted into an HTML page. These are
    // local files you may open in Notepad, so keep "Win+Alt+L" and "Café ☕"
    // readable. ("Unsafe" in the name refers to that HTML case only.)
    // https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/character-encoding
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly TimeProvider _time;
    private readonly Func<T, bool> _isUsable;

    // Only one save at a time: two overlapping saves would fight over the .tmp file.
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    /// <param name="path">Full path of the main file, e.g. ...\Ledge\tasks.json.</param>
    /// <param name="time">For the timestamp in a renamed broken file's name.</param>
    /// <param name="isUsable">Extra checks after the JSON parsed (schema version, nulls).</param>
    public JsonFile(string path, TimeProvider time, Func<T, bool> isUsable)
    {
        FilePath = path;
        _time = time;
        _isUsable = isUsable;
    }

    public string FilePath { get; }

    public async Task<(T? Value, JsonFileLoadOutcome Outcome)> LoadAsync(CancellationToken cancellationToken)
    {
        var main = await TryReadAsync(FilePath, cancellationToken).ConfigureAwait(false);
        if (main.Value is { } value)
        {
            return (value, JsonFileLoadOutcome.Loaded);
        }

        if (main.IsCorrupt)
        {
            SetAside(FilePath, "corrupt");
        }

        var backupPath = AtomicFile.BackupPath(FilePath);
        var backup = await TryReadAsync(backupPath, cancellationToken).ConfigureAwait(false);
        if (backup.Value is { } restored)
        {
            return (restored, JsonFileLoadOutcome.RestoredFromBackup);
        }

        if (backup.IsCorrupt)
        {
            SetAside(backupPath, "bak.corrupt");
        }

        return main.IsCorrupt || backup.IsCorrupt
            ? (null, JsonFileLoadOutcome.ResetAfterCorruption)
            : (null, JsonFileLoadOutcome.Missing);
    }

    public async Task SaveAsync(T value, CancellationToken cancellationToken)
    {
        await _saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await AtomicFile.WriteAsync(
                FilePath,
                (stream, token) => JsonSerializer.SerializeAsync(stream, value, JsonOptions, token),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    /// <summary>Missing → (null, not corrupt). Unusable content → (null, corrupt).</summary>
    private async Task<(T? Value, bool IsCorrupt)> TryReadAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return (null, false);
        }

        try
        {
            var stream = File.OpenRead(path);
            await using (stream.ConfigureAwait(false))
            {
                var value = await JsonSerializer
                    .DeserializeAsync<T>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);

                return value is not null && _isUsable(value) ? (value, false) : (null, true);
            }
        }
        catch (JsonException)
        {
            // Broken JSON, wrong types, an unknown enum value, or a `required` property missing.
            return (null, true);
        }
    }

    /// <summary>Renames an unusable file out of the way, e.g. tasks.corrupt-20261116-101500.json.</summary>
    private void SetAside(string path, string label)
    {
        var folder = Path.GetDirectoryName(path)!;
        var stamp = _time.GetLocalNow().ToString(CorruptStampFormat, CultureInfo.InvariantCulture);
        var baseName = $"{Path.GetFileNameWithoutExtension(FilePath)}.{label}-{stamp}";

        // Same second twice (unlikely)? Add -2, -3, … rather than overwrite.
        var target = Path.Combine(folder, baseName + ".json");
        for (var n = 2; File.Exists(target); n++)
        {
            target = Path.Combine(folder, $"{baseName}-{n}.json");
        }

        File.Move(path, target);
    }
}
