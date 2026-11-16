using Ledge.Core.Abstractions;
using Ledge.Core.Models;
using Ledge.Core.Services;
using Microsoft.Extensions.Time.Testing;

namespace Ledge.Core.Tests.Services;

public sealed class TaskAutoSaverTests : IDisposable
{
    private static readonly TimeSpan Delay = TaskAutoSaver.DefaultDelay;
    private static readonly TimeSpan JustBefore = Delay - TimeSpan.FromMilliseconds(1);

    private readonly FakeTimeProvider _time = TestClock.Create();
    private readonly FakeTaskStore _store = new();
    private readonly TaskBoard _board;
    private readonly TaskAutoSaver _saver;
    private readonly List<Exception> _failures = [];

    public TaskAutoSaverTests()
    {
        _board = new TaskBoard(_time);
        _saver = new TaskAutoSaver(_board, _store, _time);
        _saver.SaveFailed += (_, ex) => _failures.Add(ex);
    }

    public void Dispose() => _saver.Dispose();

    [Fact]
    public void NoChanges_NeverSaves()
    {
        _time.Advance(TimeSpan.FromMinutes(1));

        Assert.Empty(_store.Saved);
        Assert.False(_saver.HasUnsavedChanges);
    }

    [Fact]
    public void Change_IsSavedAfterTheDelay_NotBefore()
    {
        _board.Add("Task");

        _time.Advance(JustBefore);
        Assert.Empty(_store.Saved);
        Assert.True(_saver.HasUnsavedChanges);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Same(_board.Document, Assert.Single(_store.Saved));
        Assert.False(_saver.HasUnsavedChanges);
    }

    [Fact]
    public void QuickSuccessiveChanges_AreSavedOnce_WithLatestDocument()
    {
        _board.Add("One");
        _board.Add("Two");
        _board.Add("Three");

        _time.Advance(Delay);

        var saved = Assert.Single(_store.Saved);
        Assert.Equal(3, saved.Tasks.Count);
    }

    [Fact]
    public void EachChange_RestartsTheDelay()
    {
        _board.Add("One");
        _time.Advance(JustBefore);
        _board.Add("Two");

        _time.Advance(JustBefore);
        Assert.Empty(_store.Saved);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Single(_store.Saved);
    }

    [Fact]
    public async Task FlushAsync_SavesImmediately_AndCancelsTheTimer()
    {
        _board.Add("Task");

        await _saver.FlushAsync();
        Assert.Single(_store.Saved);

        _time.Advance(Delay);
        Assert.Single(_store.Saved); // no second save of the same data
    }

    [Fact]
    public async Task FlushAsync_WithNothingPending_DoesNotSave()
    {
        await _saver.FlushAsync();

        Assert.Empty(_store.Saved);
    }

    [Fact]
    public void FailedSave_IsReported_AndRetriedOnNextChange()
    {
        var error = new IOException("disk full");
        _store.NextError = error;
        _board.Add("One");

        _time.Advance(Delay);

        Assert.Same(error, Assert.Single(_failures));
        Assert.Empty(_store.Saved);
        Assert.True(_saver.HasUnsavedChanges);

        _board.Add("Two");
        _time.Advance(Delay);

        Assert.Equal(2, Assert.Single(_store.Saved).Tasks.Count);
        Assert.False(_saver.HasUnsavedChanges);
    }

    [Fact]
    public async Task FlushAsync_RetriesAfterFailedSave()
    {
        _store.NextError = new IOException("locked");
        _board.Add("Task");
        _time.Advance(Delay);

        await _saver.FlushAsync();

        Assert.Single(_store.Saved);
    }

    [Fact]
    public void Dispose_StopsListening()
    {
        _saver.Dispose();

        _board.Add("Task");
        _time.Advance(Delay);

        Assert.Empty(_store.Saved);
    }

    /// <summary>Records saves in memory instead of touching the disk.</summary>
    private sealed class FakeTaskStore : ITaskStore
    {
        public List<TaskDocument> Saved { get; } = [];

        /// <summary>When set, the next save throws this once.</summary>
        public Exception? NextError { get; set; }

        public Task<TaskLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new TaskLoadResult(new TaskDocument(), TaskLoadOutcome.FirstRun));

        public Task SaveAsync(TaskDocument document, CancellationToken cancellationToken = default)
        {
            if (NextError is { } error)
            {
                NextError = null;
                return Task.FromException(error);
            }

            Saved.Add(document);
            return Task.CompletedTask;
        }
    }
}
