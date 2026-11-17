using Ledge.Core.Abstractions;
using Ledge.Core.Models;

namespace Ledge.Core.Services;

/// <summary>
/// Stores <see cref="AppSettings"/> in "settings.json" next to tasks.json, with
/// the same crash-safe saving and recovery (<see cref="JsonFile{T}"/>).
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    public const string FileName = "settings.json";

    private readonly JsonFile<AppSettings> _file;

    public JsonSettingsStore(string folder, TimeProvider time)
    {
        _file = new JsonFile<AppSettings>(Path.Combine(folder, FileName), time, IsUsable);
    }

    public string FilePath => _file.FilePath;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        var (settings, _) = await _file.LoadAsync(cancellationToken).ConfigureAwait(false);
        return settings ?? new AppSettings();
    }

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
        _file.SaveAsync(settings, cancellationToken);

    /// <summary>
    /// Same rule as tasks: a newer Ledge's file is set aside, not loaded.
    /// Two values can get past the JSON reader and still be nonsense:
    /// "hotkey": null (the property isn't <c>required</c>; a MISSING hotkey just
    /// means "use the default"), and a number for the theme ("theme": 7), which
    /// the enum converter accepts even though no such theme exists.
    /// </summary>
    private static bool IsUsable(AppSettings settings) =>
        settings.SchemaVersion is >= 1 and <= AppSettings.CurrentSchemaVersion &&
        !string.IsNullOrWhiteSpace(settings.Hotkey) &&
        Enum.IsDefined(settings.Theme);
}
