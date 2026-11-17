using Ledge.Core.Models;

namespace Ledge.Core.Abstractions;

/// <summary>Loads and saves <see cref="AppSettings"/> (settings.json).</summary>
public interface ISettingsStore
{
    /// <summary>
    /// The saved settings, or the defaults if there are none yet or the file
    /// (and its backup) can't be read. Losing settings is a nuisance, not data
    /// loss, so this never fails over a broken file; I/O errors still throw.
    /// </summary>
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves atomically, keeping the previous version as settings.json.bak.</summary>
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
