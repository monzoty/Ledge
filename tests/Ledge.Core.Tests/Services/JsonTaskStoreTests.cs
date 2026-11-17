using Ledge.Core.Models;
using Ledge.Core.Services;

namespace Ledge.Core.Tests.Services;

public sealed class JsonTaskStoreTests : IDisposable
{
    private const string CorruptMainName = "tasks.corrupt-20261116-100000.json";
    private const string CorruptBackupName = "tasks.bak.corrupt-20261116-100000.json";
    private const string BrokenJson = "{ \"schemaVersion\": 1, \"tasks\": [ { \"id\": ";

    private readonly TempFolder _folder = new();
    private readonly JsonTaskStore _store;

    public JsonTaskStoreTests()
    {
        _store = new JsonTaskStore(_folder.Path, TestClock.Create());
    }

    public void Dispose() => _folder.Dispose();

    private string MainPath => _store.FilePath;

    private string BackupPath => AtomicFile.BackupPath(_store.FilePath);

    private static TaskDocument SampleDocument(string title = "Write report") => new()
    {
        Tasks =
        [
            new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = title,
                PlannedFor = TestClock.Day16,
                IsDone = true,
                CreatedAt = TestClock.Local(2026, 11, 16, 9, 0),
                CompletedAt = TestClock.Local(2026, 11, 16, 9, 30),
                SortOrder = 3,
            },
            new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = "Inbox idea",
                CreatedAt = TestClock.Local(2026, 11, 16, 9, 5),
                SortOrder = 4,
            },
        ],
    };

    // --- Normal use ----------------------------------------------------------

    [Fact]
    public async Task Load_WithNoFiles_IsFirstRun()
    {
        var result = await _store.LoadAsync();

        Assert.Equal(TaskLoadOutcome.FirstRun, result.Outcome);
        Assert.Empty(result.Document.Tasks);
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsEveryField()
    {
        var document = SampleDocument();

        await _store.SaveAsync(document);
        var result = await _store.LoadAsync();

        Assert.Equal(TaskLoadOutcome.Loaded, result.Outcome);
        Assert.Equal(document.SchemaVersion, result.Document.SchemaVersion);
        Assert.Equal(document.Tasks, result.Document.Tasks);
    }

    [Fact]
    public async Task Save_WritesReadableCamelCaseJson()
    {
        await _store.SaveAsync(SampleDocument());

        var json = await File.ReadAllTextAsync(MainPath);
        Assert.Contains("\"schemaVersion\": 1", json);
        Assert.Contains("\"plannedFor\": \"2026-11-16\"", json);
    }

    [Fact]
    public async Task Save_KeepsNonEnglishTextAndEmojiReadable()
    {
        await _store.SaveAsync(SampleDocument("Café ☕ + croissant"));

        var json = await File.ReadAllTextAsync(MainPath);
        Assert.Contains("\"title\": \"Café ☕ + croissant\"", json);
    }

    [Fact]
    public async Task Save_CreatesMissingFolder()
    {
        var store = new JsonTaskStore(Path.Combine(_folder.Path, "Ledge"), TestClock.Create());

        await store.SaveAsync(SampleDocument());

        Assert.True(File.Exists(store.FilePath));
    }

    [Fact]
    public async Task SecondSave_KeepsPreviousVersionAsBackup()
    {
        await _store.SaveAsync(SampleDocument("First"));
        await _store.SaveAsync(SampleDocument("Second"));

        Assert.Equal(["tasks.json", "tasks.json.bak"], _folder.FileNames());
        Assert.Contains("First", await File.ReadAllTextAsync(BackupPath));
        Assert.Contains("Second", await File.ReadAllTextAsync(MainPath));
    }

    [Fact]
    public async Task OverlappingSaves_DoNotCollide()
    {
        var saves = Enumerable.Range(0, 10).Select(i => _store.SaveAsync(SampleDocument($"Save {i}")));

        await Task.WhenAll(saves);

        var result = await _store.LoadAsync();
        Assert.Equal(TaskLoadOutcome.Loaded, result.Outcome);
        Assert.False(File.Exists(AtomicFile.TempPath(MainPath)));
    }

    // --- Crashes and corruption ----------------------------------------------

    [Fact]
    public async Task Load_IgnoresLeftoverTempFile()
    {
        var document = SampleDocument();
        await _store.SaveAsync(document);
        await File.WriteAllTextAsync(AtomicFile.TempPath(MainPath), BrokenJson); // crash mid-save

        var result = await _store.LoadAsync();

        Assert.Equal(TaskLoadOutcome.Loaded, result.Outcome);
        Assert.Equal(document.Tasks, result.Document.Tasks);
    }

    [Fact]
    public async Task CorruptMainFile_LoadsBackup_AndKeepsBrokenFile()
    {
        var previous = SampleDocument("Previous");
        await _store.SaveAsync(previous);
        await _store.SaveAsync(SampleDocument("Latest"));
        await File.WriteAllTextAsync(MainPath, BrokenJson);

        var result = await _store.LoadAsync();

        Assert.Equal(TaskLoadOutcome.RestoredFromBackup, result.Outcome);
        Assert.Equal(previous.Tasks, result.Document.Tasks);
        Assert.Equal(BrokenJson, await File.ReadAllTextAsync(_folder.File(CorruptMainName)));
        Assert.False(File.Exists(MainPath));
    }

    [Fact]
    public async Task BothFilesCorrupt_StartsEmpty_AndKeepsBothFiles()
    {
        await File.WriteAllTextAsync(MainPath, BrokenJson);
        await File.WriteAllTextAsync(BackupPath, "not json at all");

        var result = await _store.LoadAsync();

        Assert.Equal(TaskLoadOutcome.ResetAfterCorruption, result.Outcome);
        Assert.Empty(result.Document.Tasks);
        Assert.Equal([CorruptBackupName, CorruptMainName], _folder.FileNames());
    }

    [Fact]
    public async Task CorruptMainFile_WithoutBackup_StartsEmpty()
    {
        await File.WriteAllTextAsync(MainPath, BrokenJson);

        var result = await _store.LoadAsync();

        Assert.Equal(TaskLoadOutcome.ResetAfterCorruption, result.Outcome);
        Assert.Equal([CorruptMainName], _folder.FileNames());
    }

    [Fact]
    public async Task MissingMainFile_WithBackup_LoadsBackup()
    {
        var document = SampleDocument();
        await _store.SaveAsync(document);
        File.Move(MainPath, BackupPath);

        var result = await _store.LoadAsync();

        Assert.Equal(TaskLoadOutcome.RestoredFromBackup, result.Outcome);
        Assert.Equal(document.Tasks, result.Document.Tasks);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{ \"schemaVersion\": 1, \"tasks\": null }")]
    [InlineData("{ \"schemaVersion\": 1, \"tasks\": [ null ] }")]
    [InlineData("{ \"schemaVersion\": 0, \"tasks\": [] }")]
    [InlineData("{ \"schemaVersion\": 1, \"tasks\": [ { \"title\": \"No id or createdAt\" } ] }")]
    [InlineData("")]
    public async Task UnusableContent_IsTreatedAsCorrupt(string content)
    {
        await File.WriteAllTextAsync(MainPath, content);

        var result = await _store.LoadAsync();

        Assert.Equal(TaskLoadOutcome.ResetAfterCorruption, result.Outcome);
        Assert.True(File.Exists(_folder.File(CorruptMainName)));
    }

    [Fact]
    public async Task FileFromNewerVersion_IsSetAside_NotLoaded()
    {
        var future = $"{{ \"schemaVersion\": {TaskDocument.CurrentSchemaVersion + 1}, \"tasks\": [] }}";
        await File.WriteAllTextAsync(MainPath, future);

        var result = await _store.LoadAsync();

        Assert.Equal(TaskLoadOutcome.ResetAfterCorruption, result.Outcome);
        Assert.Equal(future, await File.ReadAllTextAsync(_folder.File(CorruptMainName)));
    }

    [Fact]
    public async Task SaveAfterRestoringBackup_WritesNewMainFile()
    {
        await _store.SaveAsync(SampleDocument("Previous"));
        await _store.SaveAsync(SampleDocument("Latest"));
        await File.WriteAllTextAsync(MainPath, BrokenJson);
        var restored = await _store.LoadAsync();

        await _store.SaveAsync(restored.Document);

        Assert.Equal(TaskLoadOutcome.Loaded, (await _store.LoadAsync()).Outcome);
        Assert.True(File.Exists(_folder.File(CorruptMainName)));
    }

    [Fact]
    public async Task CorruptFileName_DoesNotOverwriteEarlierOne()
    {
        await File.WriteAllTextAsync(_folder.File(CorruptMainName), "earlier");
        await File.WriteAllTextAsync(MainPath, BrokenJson);

        await _store.LoadAsync();

        Assert.Equal("earlier", await File.ReadAllTextAsync(_folder.File(CorruptMainName)));
        Assert.True(File.Exists(_folder.File("tasks.corrupt-20261116-100000-2.json")));
    }
}
