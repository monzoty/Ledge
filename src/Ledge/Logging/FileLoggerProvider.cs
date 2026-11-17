using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Ledge.Logging;

/// <summary>
/// Writes log lines to %LOCALAPPDATA%\Ledge\logs\ledge-&lt;date&gt;.log, one file
/// per local day, keeping the newest <see cref="FilesToKeep"/> (ARCHITECTURE.md §8).
/// </summary>
/// <remarks>
/// An ILoggerProvider is the plug-in point of .NET logging: every
/// <c>ILogger&lt;T&gt;</c> in the app sends its lines to all registered providers.
/// https://learn.microsoft.com/dotnet/core/extensions/custom-logging-provider
///
/// Lines are put in a queue (a Channel) and ONE background task writes them,
/// so logging never waits for the disk on the UI thread, and lines from
/// different threads can't interleave.
/// https://learn.microsoft.com/dotnet/core/extensions/channels
///
/// PRIVACY RULE (ARCHITECTURE.md §8): never log task titles or other user
/// content. Log ids, counts and what happened, not what the user wrote.
/// </remarks>
public sealed class FileLoggerProvider : ILoggerProvider
{
    public const int FilesToKeep = 7;

    private const string FilePrefix = "ledge-";
    private const string FileExtension = ".log";
    private const string DateFormat = "yyyy-MM-dd";
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz";

    /// <summary>ADR-0005: the longest Dispose waits for queued lines to reach the disk.</summary>
    private static readonly TimeSpan DisposeFlushTimeout = TimeSpan.FromSeconds(1);

    private readonly string _folder;
    private readonly TimeProvider _time;
    private readonly Channel<QueueItem> _queue = Channel.CreateUnbounded<QueueItem>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Task _writer;
    private string? _currentFile;

    public FileLoggerProvider(string folder, TimeProvider time)
    {
        _folder = folder;
        _time = time;
        _writer = Task.Run(WriteLoopAsync);
    }

    /// <summary>Where the log files go; shown in docs and handy in tests.</summary>
    public string Folder => _folder;

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    /// <summary>Completes once every line queued so far is on disk.</summary>
    public Task FlushAsync()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_queue.Writer.TryWrite(QueueItem.Flush(done)))
        {
            done.TrySetResult(); // already shut down: nothing more can be written
        }

        return done.Task;
    }

    /// <summary>
    /// Stops accepting lines and lets the writer finish what's queued, waiting
    /// at most <see cref="DisposeFlushTimeout"/>. Called on exit and after a
    /// crash, where there's no UI left to freeze (ADR-0005).
    /// </summary>
    public void Dispose()
    {
        _queue.Writer.TryComplete();
        _writer.Wait(DisposeFlushTimeout);
    }

    /// <summary>Called by FileLogger: stamps and queues one line (never blocks).</summary>
    internal void Enqueue(LogLevel level, string category, string message, Exception? exception)
    {
        var now = _time.GetLocalNow();
        var line = new StringBuilder()
            .Append(now.ToString(TimestampFormat, CultureInfo.InvariantCulture))
            .Append(" [").Append(LevelCode(level)).Append("] ")
            .Append(category).Append(": ").Append(message);

        if (exception is not null)
        {
            // ToString() includes the type, message and stack trace (and inner exceptions).
            line.AppendLine().Append("    ").Append(exception.ToString().Replace("\n", "\n    ", StringComparison.Ordinal));
        }

        _queue.Writer.TryWrite(QueueItem.Line(DateOnly.FromDateTime(now.DateTime), line.ToString()));
    }

    /// <summary>Three-letter level codes keep the columns lined up.</summary>
    private static string LevelCode(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "???",
    };

    /// <summary>The single writer: takes items off the queue, one at a time, until Dispose.</summary>
    private async Task WriteLoopAsync()
    {
        await foreach (var item in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            if (item.FlushDone is { } flushDone)
            {
                flushDone.TrySetResult();
                continue;
            }

            try
            {
                await AppendAsync(item.Day, item.Text!).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // A logger must never take the app down: drop the line.
            }
            catch (UnauthorizedAccessException)
            {
                // Same: e.g. the folder became read-only.
            }
        }
    }

    private async Task AppendAsync(DateOnly day, string text)
    {
        var path = Path.Combine(_folder, FilePrefix + day.ToString(DateFormat, CultureInfo.InvariantCulture) + FileExtension);

        // First line of a new day (or of this run): make sure the folder
        // exists and drop the oldest files.
        if (path != _currentFile)
        {
            Directory.CreateDirectory(_folder);
            _currentFile = path;
            DeleteOldFiles();
        }

        await File.AppendAllTextAsync(path, text + Environment.NewLine, new UTF8Encoding(false)).ConfigureAwait(false);
    }

    /// <summary>
    /// Keeps the newest <see cref="FilesToKeep"/> log files (today's included).
    /// The date in the name sorts correctly as text, so no parsing is needed.
    /// </summary>
    private void DeleteOldFiles()
    {
        // Today's file always stays; of the others, keep the newest FilesToKeep − 1.
        var old = Directory.GetFiles(_folder, FilePrefix + "*" + FileExtension)
            .Where(file => !string.Equals(file, _currentFile, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .SkipLast(FilesToKeep - 1);

        foreach (var file in old)
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // Open in an editor, perhaps. Try again next time.
            }
        }
    }

    /// <summary>One queue entry: a line to write, or a flush marker.</summary>
    private readonly record struct QueueItem(DateOnly Day, string? Text, TaskCompletionSource? FlushDone)
    {
        public static QueueItem Line(DateOnly day, string text) => new(day, text, null);

        public static QueueItem Flush(TaskCompletionSource done) => new(default, null, done);
    }
}
