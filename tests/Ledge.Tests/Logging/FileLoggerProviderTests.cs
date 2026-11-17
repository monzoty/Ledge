using System.IO;
using Ledge.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Ledge.Tests.Logging;

public sealed class FileLoggerProviderTests : IDisposable
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(5.5);

    // 17 Nov 2026, 10:15 local (UTC+05:30). FakeTimeProvider wants UTC.
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 17, 10, 15, 0, Offset).ToUniversalTime());
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "Ledge.Tests", Guid.NewGuid().ToString("N"));
    private readonly FileLoggerProvider _provider;
    private readonly ILogger _logger;

    public FileLoggerProviderTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("Test+05:30", Offset, "Test+05:30", "Test+05:30"));
        _provider = new FileLoggerProvider(_folder, _time);
        _logger = _provider.CreateLogger("Ledge.Test");
    }

    public void Dispose()
    {
        _provider.Dispose();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private string LogFile(string date) => Path.Combine(_folder, $"ledge-{date}.log");

    private string[] LogFileNames() =>
        [.. Directory.GetFiles(_folder).Select(Path.GetFileName).OfType<string>().Order()];

    [Fact]
    public async Task Line_HasTimestampLevelCategoryAndMessage()
    {
        _logger.LogWarning("Hotkey {Hotkey} is taken", "Win+Alt+L");
        await _provider.FlushAsync();

        var line = Assert.Single(await File.ReadAllLinesAsync(LogFile("2026-11-17")));
        Assert.Equal("2026-11-17 10:15:00.000 +05:30 [WRN] Ledge.Test: Hotkey Win+Alt+L is taken", line);
    }

    [Fact]
    public async Task Exception_IsWrittenIndentedUnderTheLine()
    {
        _logger.LogError(new InvalidOperationException("boom"), "Something failed");
        await _provider.FlushAsync();

        var lines = await File.ReadAllLinesAsync(LogFile("2026-11-17"));
        Assert.EndsWith("[ERR] Ledge.Test: Something failed", lines[0]);
        Assert.StartsWith("    System.InvalidOperationException: boom", lines[1]);
    }

    [Fact]
    public async Task Lines_AreAppendedInOrder()
    {
        _logger.LogInformation("one");
        _logger.LogInformation("two");
        _logger.LogInformation("three");
        await _provider.FlushAsync();

        var messages = (await File.ReadAllLinesAsync(LogFile("2026-11-17"))).Select(l => l[(l.LastIndexOf(':') + 2)..]);
        Assert.Equal(["one", "two", "three"], messages);
    }

    [Fact]
    public async Task NewDay_StartsANewFile()
    {
        _logger.LogInformation("today");
        _time.Advance(TimeSpan.FromDays(1));
        _logger.LogInformation("tomorrow");
        await _provider.FlushAsync();

        Assert.Equal(["ledge-2026-11-17.log", "ledge-2026-11-18.log"], LogFileNames());
    }

    [Fact]
    public async Task OnlyTheNewestSevenFiles_AreKept()
    {
        Directory.CreateDirectory(_folder);
        for (var day = 1; day <= 10; day++)
        {
            await File.WriteAllTextAsync(LogFile($"2026-11-{day:00}"), "old");
        }

        _logger.LogInformation("today");
        await _provider.FlushAsync();

        Assert.Equal(
            [
                "ledge-2026-11-05.log", "ledge-2026-11-06.log", "ledge-2026-11-07.log",
                "ledge-2026-11-08.log", "ledge-2026-11-09.log", "ledge-2026-11-10.log",
                "ledge-2026-11-17.log",
            ],
            LogFileNames());
    }

    [Fact]
    public async Task OtherFilesInTheFolder_AreLeftAlone()
    {
        Directory.CreateDirectory(_folder);
        await File.WriteAllTextAsync(Path.Combine(_folder, "notes.txt"), "mine");

        _logger.LogInformation("today");
        await _provider.FlushAsync();

        Assert.Contains("notes.txt", LogFileNames());
    }

    [Fact]
    public async Task FlushWithNothingQueued_Completes()
    {
        await _provider.FlushAsync();

        Assert.False(Directory.Exists(_folder)); // nothing written, so no folder either
    }

    [Fact]
    public void Dispose_WritesWhatWasQueued()
    {
        _logger.LogInformation("last words");

        _provider.Dispose();

        Assert.Contains("last words", File.ReadAllText(LogFile("2026-11-17")));
    }

    [Fact]
    public async Task LoggingAfterDispose_IsIgnored()
    {
        _provider.Dispose();

        _logger.LogInformation("too late");
        await _provider.FlushAsync(); // must not hang

        Assert.False(Directory.Exists(_folder));
    }
}
