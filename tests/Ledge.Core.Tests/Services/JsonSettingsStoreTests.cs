using Ledge.Core.Models;
using Ledge.Core.Services;

namespace Ledge.Core.Tests.Services;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private const string CorruptMainName = "settings.corrupt-20261116-100000.json";
    private const string CorruptBackupName = "settings.bak.corrupt-20261116-100000.json";

    private readonly TempFolder _folder = new();
    private readonly JsonSettingsStore _store;

    public JsonSettingsStoreTests()
    {
        _store = new JsonSettingsStore(_folder.Path, TestClock.Create());
    }

    public void Dispose() => _folder.Dispose();

    private string MainPath => _store.FilePath;

    private static AppSettings Custom => new()
    {
        Hotkey = "Ctrl+Alt+Shift+L",
        StartWithWindows = true,
        Theme = ThemeMode.Light,
    };

    // --- Defaults ----------------------------------------------------------------

    [Fact]
    public void Defaults_MatchTheDesign()
    {
        var defaults = new AppSettings();

        Assert.Equal("Win+Alt+L", defaults.Hotkey);           // DESIGN.md §5
        Assert.False(defaults.StartWithWindows);             // PRD F5.3
        Assert.Equal(ThemeMode.System, defaults.Theme);      // PRD F6
        Assert.Equal(AppSettings.CurrentSchemaVersion, defaults.SchemaVersion);
    }

    [Fact]
    public async Task Load_WithNoFile_ReturnsDefaults_AndCreatesNothing()
    {
        var settings = await _store.LoadAsync();

        Assert.Equal(new AppSettings(), settings);
        Assert.Empty(_folder.FileNames());
    }

    // --- Normal use ---------------------------------------------------------------

    [Fact]
    public async Task SaveThenLoad_RoundTripsEverySetting()
    {
        await _store.SaveAsync(Custom);

        Assert.Equal(Custom, await _store.LoadAsync());
    }

    [Fact]
    public async Task Save_WritesReadableJson_WithThemeAsText()
    {
        await _store.SaveAsync(Custom);

        var json = await File.ReadAllTextAsync(MainPath);
        Assert.Contains("\"hotkey\": \"Ctrl+Alt+Shift+L\"", json);
        Assert.Contains("\"startWithWindows\": true", json);
        Assert.Contains("\"theme\": \"light\"", json);
    }

    [Fact]
    public async Task SecondSave_KeepsPreviousVersionAsBackup()
    {
        await _store.SaveAsync(new AppSettings());
        await _store.SaveAsync(Custom);

        Assert.Equal(["settings.json", "settings.json.bak"], _folder.FileNames());
    }

    [Fact]
    public async Task MissingProperties_GetTheirDefaults()
    {
        // e.g. a file written by an older Ledge, or edited by hand.
        await File.WriteAllTextAsync(MainPath, "{ \"schemaVersion\": 1, \"startWithWindows\": true }");

        var settings = await _store.LoadAsync();

        Assert.True(settings.StartWithWindows);
        Assert.Equal(AppSettings.DefaultHotkey, settings.Hotkey);
        Assert.Equal(ThemeMode.System, settings.Theme);
    }

    [Theory]
    [InlineData("system", ThemeMode.System)]
    [InlineData("dark", ThemeMode.Dark)]
    [InlineData("Dark", ThemeMode.Dark)] // reading ignores case
    public async Task Theme_IsReadFromText(string text, ThemeMode expected)
    {
        await File.WriteAllTextAsync(MainPath, $"{{ \"schemaVersion\": 1, \"theme\": \"{text}\" }}");

        Assert.Equal(expected, (await _store.LoadAsync()).Theme);
    }

    // --- Broken files -------------------------------------------------------------

    [Fact]
    public async Task CorruptFile_LoadsBackup()
    {
        await _store.SaveAsync(Custom);
        await _store.SaveAsync(new AppSettings());
        await File.WriteAllTextAsync(MainPath, "{ broken");

        var settings = await _store.LoadAsync();

        Assert.Equal(Custom, settings);
        Assert.True(File.Exists(_folder.File(CorruptMainName)));
    }

    [Fact]
    public async Task BothCorrupt_GiveDefaults_AndKeepBothFiles()
    {
        await File.WriteAllTextAsync(MainPath, "{ broken");
        await File.WriteAllTextAsync(AtomicFile.BackupPath(MainPath), "also broken");

        var settings = await _store.LoadAsync();

        Assert.Equal(new AppSettings(), settings);
        Assert.Equal([CorruptBackupName, CorruptMainName], _folder.FileNames());
    }

    [Theory]
    [InlineData("{ \"schemaVersion\": 1, \"hotkey\": null }")]
    [InlineData("{ \"schemaVersion\": 1, \"hotkey\": \"   \" }")]
    [InlineData("{ \"schemaVersion\": 1, \"theme\": \"purple\" }")]
    [InlineData("{ \"schemaVersion\": 1, \"theme\": 7 }")]
    [InlineData("{ \"schemaVersion\": 0 }")]
    [InlineData("{ \"schemaVersion\": 2 }")] // written by a newer Ledge
    [InlineData("null")]
    public async Task UnusableContent_GivesDefaults_AndIsSetAside(string content)
    {
        await File.WriteAllTextAsync(MainPath, content);

        var settings = await _store.LoadAsync();

        Assert.Equal(new AppSettings(), settings);
        Assert.Equal(content, await File.ReadAllTextAsync(_folder.File(CorruptMainName)));
    }
}
