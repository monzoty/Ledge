using Ledge.Core.Abstractions;
using Ledge.Core.Models;
using Ledge.Core.Services;

namespace Ledge.Core.Tests.Services;

public sealed class SettingsServiceTests
{
    private readonly FakeSettingsStore _store = new();
    private readonly SettingsService _service;
    private readonly List<AppSettings> _changes = [];
    private readonly List<Exception> _failures = [];

    public SettingsServiceTests()
    {
        _service = new SettingsService(_store, new AppSettings());
        _service.Changed += (_, settings) => _changes.Add(settings);
        _service.SaveFailed += (_, ex) => _failures.Add(ex);
    }

    [Fact]
    public void Current_StartsWithGivenSettings()
    {
        Assert.Equal(new AppSettings(), _service.Current);
    }

    [Fact]
    public async Task Update_ChangesCurrent_RaisesChanged_AndSaves()
    {
        await _service.UpdateAsync(s => s with { Theme = ThemeMode.Light });

        Assert.Equal(ThemeMode.Light, _service.Current.Theme);
        Assert.Equal(ThemeMode.Light, Assert.Single(_changes).Theme);
        Assert.Equal(ThemeMode.Light, Assert.Single(_store.Saved).Theme);
    }

    [Fact]
    public async Task Update_WithNoRealChange_DoesNothing()
    {
        await _service.UpdateAsync(s => s with { Theme = ThemeMode.System });

        Assert.Empty(_changes);
        Assert.Empty(_store.Saved);
    }

    [Fact]
    public async Task Updates_Accumulate()
    {
        await _service.UpdateAsync(s => s with { StartWithWindows = true });
        await _service.UpdateAsync(s => s with { Hotkey = "Ctrl+Alt+Space" });

        Assert.True(_service.Current.StartWithWindows);
        Assert.Equal("Ctrl+Alt+Space", _service.Current.Hotkey);
        Assert.Equal(_service.Current, _store.Saved[^1]);
    }

    [Fact]
    public async Task FailedSave_IsReported_ButChangeStillApplies()
    {
        _store.NextError = new IOException("disk full");

        await _service.UpdateAsync(s => s with { StartWithWindows = true });

        Assert.True(_service.Current.StartWithWindows);
        Assert.IsType<IOException>(Assert.Single(_failures));
    }

    private sealed class FakeSettingsStore : ISettingsStore
    {
        public List<AppSettings> Saved { get; } = [];

        public Exception? NextError { get; set; }

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AppSettings());

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            if (NextError is { } error)
            {
                NextError = null;
                return Task.FromException(error);
            }

            Saved.Add(settings);
            return Task.CompletedTask;
        }
    }
}
