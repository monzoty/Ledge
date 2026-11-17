using Ledge.Core.Abstractions;
using Ledge.Core.Models;

namespace Ledge.Core.Services;

/// <summary>
/// The app's live settings: the single owner of the current
/// <see cref="AppSettings"/>, like TaskBoard is for tasks. Every change is
/// applied in memory, announced through <see cref="Changed"/>, then saved.
/// </summary>
public sealed class SettingsService
{
    private readonly ISettingsStore _store;

    public SettingsService(ISettingsStore store, AppSettings initial)
    {
        _store = store;
        Current = initial;
    }

    /// <summary>The new settings, after a change. Raised on the caller's thread, before saving.</summary>
    public event EventHandler<AppSettings>? Changed;

    /// <summary>
    /// Saving failed (disk full, access denied…). The change still applies for
    /// this session; it just won't survive a restart unless a later save works.
    /// </summary>
    public event EventHandler<Exception>? SaveFailed;

    public AppSettings Current { get; private set; }

    /// <summary>
    /// Applies <paramref name="change"/> (e.g. <c>s => s with { Theme = ThemeMode.Light }</c>)
    /// and saves. Does nothing if the result equals the current settings.
    /// Never throws for a failed save; see <see cref="SaveFailed"/>.
    /// </summary>
    public async Task UpdateAsync(Func<AppSettings, AppSettings> change)
    {
        var updated = change(Current);
        if (updated == Current)
        {
            return;
        }

        Current = updated;
        Changed?.Invoke(this, updated);

        try
        {
            await _store.SaveAsync(updated).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Deliberately broad: this is called from UI commands, where an
            // unhandled exception would crash the app over a lost preference.
            SaveFailed?.Invoke(this, ex);
        }
    }
}
