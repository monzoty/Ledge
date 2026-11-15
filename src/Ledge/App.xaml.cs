using System.Windows;
using Ledge.Windows;

namespace Ledge;

/// <summary>
/// Application entry point: makes sure only one copy runs, then shows the pill.
/// (The Generic Host / DI setup arrives in M3.)
/// </summary>
public sealed partial class App : Application
{
    /// <summary>
    /// "Local\" scopes the name to the current Windows user session, so two
    /// people signed in on the same PC can each run their own Ledge.
    /// https://learn.microsoft.com/windows/win32/termserv/kernel-object-namespaces
    /// </summary>
    private const string SingleInstanceMutexName = @"Local\Ledge-SingleInstance";

    /// <summary>
    /// Kept in a field on purpose: if it were a local variable, the garbage
    /// collector could free it and release the lock while the app is still running.
    /// </summary>
    private Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Try to create (and own) the named mutex. If it already exists,
        // another Ledge is running in this session.
        // https://learn.microsoft.com/dotnet/api/system.threading.mutex.-ctor#system-threading-mutex-ctor(system-boolean-system-string-system-boolean@)
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);

        if (!createdNew)
        {
            // M4: tell the running copy to open its panel before exiting.
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }

        new PillWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_singleInstanceMutex is not null)
        {
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
        }

        base.OnExit(e);
    }
}
