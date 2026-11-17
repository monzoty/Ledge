using System.IO;
using System.Windows.Threading;
using Ledge.Core.Models;
using Ledge.Logging;
using Microsoft.Extensions.Logging;

namespace Ledge;

/// <summary>
/// Logging and "last resort" error handling (ARCHITECTURE.md §8).
/// A partial class: the same App, split into a second file for readability.
/// </summary>
/// <remarks>
/// .NET has three places where an exception nobody caught ends up:
/// <list type="bullet">
/// <item><b>Application.DispatcherUnhandledException</b>: thrown on the UI thread
/// (most bugs in a WPF app).
/// https://learn.microsoft.com/dotnet/api/system.windows.application.dispatcherunhandledexception</item>
/// <item><b>AppDomain.UnhandledException</b>: thrown on any other thread. The
/// process always ends after this one.
/// https://learn.microsoft.com/dotnet/api/system.appdomain.unhandledexception</item>
/// <item><b>TaskScheduler.UnobservedTaskException</b>: a background Task failed and
/// nobody awaited it. Doesn't end the process in modern .NET.
/// https://learn.microsoft.com/dotnet/api/system.threading.tasks.taskscheduler.unobservedtaskexception</item>
/// </list>
/// Decided 2026-11-17: on a real crash, log it, save pending tasks, then let
/// Ledge close. Never keep running in a half-broken state.
/// </remarks>
public sealed partial class App
{
    private const string LogsFolderName = "logs";

    private FileLoggerProvider? _logs;
    private ILogger _log = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    /// <summary>Set once a crash is being handled; the UI-thread handler is often followed by the AppDomain one.</summary>
    private int _isCrashing;

    private static string LogsFolder => Path.Combine(DataFolder, LogsFolderName);

    /// <summary>Starts logging before anything else, so even a failed start-up is recorded.</summary>
    private void StartLogging(TimeProvider time)
    {
        _logs = new FileLoggerProvider(LogsFolder, time);
        _log = _logs.CreateLogger(typeof(App).FullName!);

        DispatcherUnhandledException += OnUiThreadException;
        AppDomain.CurrentDomain.UnhandledException += OnAnyThreadException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _log.LogInformation("Ledge {Version} started", typeof(App).Assembly.GetName().Version);
    }

    /// <summary>
    /// Plugs the file logger into the Generic Host's logging, so every
    /// ILogger&lt;T&gt; in the app writes to the same files.
    /// </summary>
    private void ConfigureLogging(ILoggingBuilder logging)
    {
        // The defaults include a console (useless in a windowed app) and the
        // Windows Event Log (not wanted). Keep only the debugger output + our file.
        logging.ClearProviders();
        logging.AddDebug();
        logging.AddProvider(_logs!);

        logging.SetMinimumLevel(LogLevel.Information);

        // .NET's own libraries are chatty at Information; only their problems matter.
        logging.AddFilter("Microsoft", LogLevel.Warning);
    }

    /// <summary>Counts and outcomes only; never titles (privacy rule, ARCHITECTURE.md §8).</summary>
    private void LogLoad(TaskLoadResult loaded)
    {
        switch (loaded.Outcome)
        {
            case TaskLoadOutcome.RestoredFromBackup:
                _log.LogWarning("tasks.json couldn't be read; restored the backup ({Count} tasks)", loaded.Document.Tasks.Count);
                break;
            case TaskLoadOutcome.ResetAfterCorruption:
                _log.LogError("tasks.json and its backup couldn't be read; started empty (damaged files kept, renamed)");
                break;
            case TaskLoadOutcome.FirstRun:
                _log.LogInformation("No saved tasks yet (first run)");
                break;
            default:
                _log.LogInformation("Loaded {Count} tasks", loaded.Document.Tasks.Count);
                break;
        }
    }

    private void OnUiThreadException(object sender, DispatcherUnhandledExceptionEventArgs e) =>
        HandleCrash(e.Exception, "UI thread");

    private void OnAnyThreadException(object sender, UnhandledExceptionEventArgs e) =>
        HandleCrash(e.ExceptionObject as Exception, "background thread");

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _log.LogError(e.Exception, "A background task failed and nobody was waiting for it");

        // "Observed" = we've dealt with it; the app keeps running.
        e.SetObserved();
    }

    /// <summary>
    /// Log it, save pending tasks, get the log onto the disk, then let .NET
    /// close Ledge as usual (we don't mark the exception as handled).
    /// </summary>
    private void HandleCrash(Exception? exception, string where)
    {
        if (Interlocked.Exchange(ref _isCrashing, 1) == 1)
        {
            return;
        }

        _log.LogCritical(exception, "Unexpected error on the {Where}; Ledge will close", where);

        try
        {
            FlushPendingSave();
        }
        catch (Exception saveError)
        {
            _log.LogError(saveError, "Saving tasks while closing after an error failed");
        }

        // ADR-0005: bounded wait for the queued log lines.
        _logs?.Dispose();
    }
}
