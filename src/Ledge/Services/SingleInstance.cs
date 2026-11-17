using Ledge.Interop;

namespace Ledge.Services;

/// <summary>
/// Makes sure only one Ledge runs per Windows session, and lets a second
/// launch ask the running copy to open its panel (PRD F5.2).
/// </summary>
/// <remarks>
/// Two named kernel objects, visible to every process in this session:
/// <list type="bullet">
/// <item>a <b>Mutex</b>: whoever creates it first is "the" Ledge;</item>
/// <item>an <b>EventWaitHandle</b>: the running copy waits on it, and a second
/// copy sets it ("signals" it) before exiting.</item>
/// </list>
/// "Local\" scopes both names to the current user session, so two people signed
/// in on the same PC can each run their own Ledge.
/// https://learn.microsoft.com/windows/win32/termserv/kernel-object-namespaces
/// https://learn.microsoft.com/dotnet/api/system.threading.eventwaithandle
/// </remarks>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\Ledge-SingleInstance";
    private const string OpenPanelEventName = @"Local\Ledge-OpenPanel";

    /// <summary>
    /// Kept in a field on purpose: if it were only a local variable, the garbage
    /// collector could free it and release the lock while the app is still running.
    /// </summary>
    private Mutex? _mutex;

    private EventWaitHandle? _openPanelEvent;
    private RegisteredWaitHandle? _registeredWait;

    public SingleInstance()
    {
        // Try to create (and own) the named mutex. If it already exists,
        // another Ledge is running in this session.
        // https://learn.microsoft.com/dotnet/api/system.threading.mutex.-ctor#system-threading-mutex-ctor(system-boolean-system-string-system-boolean@)
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        IsFirstInstance = createdNew;

        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
        }
    }

    /// <summary>
    /// Another launch asked us to open the panel. Raised on a thread-pool
    /// thread: hand it over to the UI thread before touching windows.
    /// </summary>
    public event EventHandler? OpenPanelRequested;

    public bool IsFirstInstance { get; }

    /// <summary>
    /// First instance only: start listening for later launches. The thread
    /// pool waits on the event for us, so no thread of our own sits blocked.
    /// https://learn.microsoft.com/dotnet/api/system.threading.threadpool.registerwaitforsingleobject
    /// </summary>
    public void ListenForOtherLaunches()
    {
        _openPanelEvent = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, OpenPanelEventName);
        _registeredWait = ThreadPool.RegisterWaitForSingleObject(
            _openPanelEvent,
            (_, _) => OpenPanelRequested?.Invoke(this, EventArgs.Empty),
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    /// <summary>
    /// Second instance: tell the running copy to open its panel. If it's still
    /// starting up (no event yet), there's nobody to tell; nothing happens.
    /// </summary>
    public static void AskRunningInstanceToOpenPanel()
    {
        if (!EventWaitHandle.TryOpenExisting(OpenPanelEventName, out var openPanelEvent))
        {
            return;
        }

        using (openPanelEvent)
        {
            NativeMethods.AllowSetForegroundWindow(NativeMethods.AsfwAny);
            openPanelEvent.Set();
        }
    }

    public void Dispose()
    {
        _registeredWait?.Unregister(null);
        _openPanelEvent?.Dispose();

        if (_mutex is not null)
        {
            // Must run on the thread that created (owns) the mutex: the UI thread.
            _mutex.ReleaseMutex();
            _mutex.Dispose();
            _mutex = null;
        }
    }
}
