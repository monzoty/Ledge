using Ledge.Core.Abstractions;

namespace Ledge.Core.Services;

/// <summary>
/// Saves the board automatically (PRD F3.9): after every change, once nothing
/// has changed for <see cref="DefaultDelay"/>. Typing five tasks quickly
/// therefore writes the file once, not five times ("debouncing").
/// </summary>
/// <remarks>
/// Threading: Changed arrives on the UI thread, but the timer fires on a
/// thread-pool thread. That's safe because the board's Document is an
/// immutable object that is only ever swapped as a whole, so reading it from
/// another thread always sees a complete document. <see cref="SaveFailed"/>
/// is raised on a background thread.
/// </remarks>
public sealed class TaskAutoSaver : IDisposable
{
    /// <summary>ARCHITECTURE.md §5: "debounced to about 300 ms".</summary>
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromMilliseconds(300);

    private readonly TaskBoard _board;
    private readonly ITaskStore _store;
    private readonly TimeSpan _delay;
    private readonly ITimer _timer;

    // One save at a time, in order. Whoever gets in saves the LATEST document,
    // so the file can never end up with an older version than memory.
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    // Read and written from the UI thread and the timer thread; `volatile`
    // makes sure each thread sees the other's latest write.
    private volatile bool _hasUnsavedChanges;

    public TaskAutoSaver(TaskBoard board, ITaskStore store, TimeProvider time)
        : this(board, store, time, DefaultDelay)
    {
    }

    public TaskAutoSaver(TaskBoard board, ITaskStore store, TimeProvider time, TimeSpan delay)
    {
        _board = board;
        _store = store;
        _delay = delay;

        // A TimeProvider timer, so tests can fire it with FakeTimeProvider.Advance.
        // Created stopped (InfiniteTimeSpan); each change (re)starts it.
        // https://learn.microsoft.com/dotnet/api/system.timeprovider.createtimer
        _timer = time.CreateTimer(_ => _ = SaveNowAsync(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        _board.Changed += OnBoardChanged;
    }

    /// <summary>
    /// A save went wrong (disk full, access denied…). The data is still in
    /// memory and the next change tries again (ARCHITECTURE.md §8).
    /// </summary>
    public event EventHandler<Exception>? SaveFailed;

    public bool HasUnsavedChanges => _hasUnsavedChanges;

    /// <summary>
    /// Saves pending changes right now (on Quit / shutdown) and waits for any
    /// save already running. Never throws; failures go to <see cref="SaveFailed"/>.
    /// </summary>
    public Task FlushAsync()
    {
        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        return SaveNowAsync();
    }

    public void Dispose()
    {
        _board.Changed -= OnBoardChanged;
        _timer.Dispose();
    }

    private void OnBoardChanged(object? sender, EventArgs e)
    {
        _hasUnsavedChanges = true;

        // Restart the countdown: the save happens _delay after the LAST change.
        _timer.Change(_delay, Timeout.InfiniteTimeSpan);
    }

    private async Task SaveNowAsync()
    {
        await _saveGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_hasUnsavedChanges)
            {
                return; // nothing new, or another save already wrote it
            }

            _hasUnsavedChanges = false;
            var document = _board.Document;

            try
            {
                await _store.SaveAsync(document).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Catching everything is deliberate: this runs from a timer, where an
                // unhandled exception would be lost (or crash the app). Mark it
                // unsaved so the next change or flush retries, and report it.
                _hasUnsavedChanges = true;
                SaveFailed?.Invoke(this, ex);
            }
        }
        finally
        {
            _saveGate.Release();
        }
    }
}
