using Microsoft.Extensions.Logging;

namespace Ledge.Logging;

/// <summary>
/// The ILogger handed to one category (usually one class). It only formats the
/// message and passes it to <see cref="FileLoggerProvider"/>, which does the
/// writing. Which levels get through is decided by the logging filters set up
/// in App (Information and above; Microsoft's own messages from Warning).
/// </summary>
internal sealed class FileLogger : ILogger
{
    private readonly FileLoggerProvider _provider;
    private readonly string _category;

    public FileLogger(FileLoggerProvider provider, string category)
    {
        _provider = provider;
        _category = category;
    }

    /// <summary>Scopes add context to a group of lines; this simple logger doesn't use them.</summary>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        _provider.Enqueue(logLevel, _category, formatter(state, exception), exception);
    }
}
