namespace Ledge.Core.Services;

/// <summary>
/// Crash-safe file writes (ARCHITECTURE.md §5). Used for tasks.json now and
/// settings.json in M4.
/// </summary>
/// <remarks>
/// Writing straight into tasks.json first empties it; a crash or power cut at
/// that moment loses everything. Instead:
/// <list type="number">
/// <item>write the new content to "tasks.json.tmp" and force it onto the disk;</item>
/// <item>swap it in with ONE call, File.Replace, which also keeps the old
/// file as "tasks.json.bak".</item>
/// </list>
/// At every moment either the old or the new tasks.json exists in full.
/// </remarks>
public static class AtomicFile
{
    private const int BufferSize = 4096;

    public static string TempPath(string path) => path + ".tmp";

    public static string BackupPath(string path) => path + ".bak";

    /// <summary>
    /// Replaces <paramref name="path"/> with whatever <paramref name="write"/>
    /// writes to the stream. The previous file (if any) becomes the .bak.
    /// </summary>
    public static async Task WriteAsync(
        string path,
        Func<Stream, CancellationToken, Task> write,
        CancellationToken cancellationToken = default)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(folder); // does nothing if it already exists

        var tempPath = TempPath(path);

        // FileOptions.Asynchronous: real async I/O, so no thread sits waiting on the disk.
        // ConfigureAwait(false): Core is a library; continue on any thread instead of
        // queueing back onto the UI thread. That also keeps the synchronous Flush and
        // Replace below off the UI thread.
        // https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2007
        var stream = new FileStream(
            tempPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous);
        await using (stream.ConfigureAwait(false))
        {
            await write(stream, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            // FlushAsync only empties .NET's buffer into Windows' cache. Flush(true)
            // makes Windows write it to the disk itself (FlushFileBuffers), so a
            // power cut can't leave a "finished" temp file that is really empty.
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(path))
        {
            // Atomic swap: tmp → path, old path → .bak (overwriting the older .bak).
            // https://learn.microsoft.com/dotnet/api/system.io.file.replace
            File.Replace(tempPath, path, BackupPath(path), ignoreMetadataErrors: true);
        }
        else
        {
            // First save (or the main file was set aside as corrupt): nothing to replace.
            File.Move(tempPath, path);
        }
    }
}
