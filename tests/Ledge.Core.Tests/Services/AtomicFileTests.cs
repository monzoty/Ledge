using System.Text;
using Ledge.Core.Services;

namespace Ledge.Core.Tests.Services;

public sealed class AtomicFileTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private static Task WriteTextAsync(string path, string text) =>
        AtomicFile.WriteAsync(path, (stream, token) => stream.WriteAsync(Encoding.UTF8.GetBytes(text), token).AsTask());

    [Fact]
    public async Task FirstWrite_CreatesFile_WithoutBackupOrTemp()
    {
        var path = _folder.File("data.json");

        await WriteTextAsync(path, "one");

        Assert.Equal("one", await File.ReadAllTextAsync(path));
        Assert.Equal(["data.json"], _folder.FileNames());
    }

    [Fact]
    public async Task SecondWrite_KeepsPreviousVersionAsBackup()
    {
        var path = _folder.File("data.json");

        await WriteTextAsync(path, "one");
        await WriteTextAsync(path, "two");

        Assert.Equal("two", await File.ReadAllTextAsync(path));
        Assert.Equal("one", await File.ReadAllTextAsync(AtomicFile.BackupPath(path)));
        Assert.Equal(["data.json", "data.json.bak"], _folder.FileNames());
    }

    [Fact]
    public async Task Write_CreatesMissingFolder()
    {
        var path = Path.Combine(_folder.Path, "a", "b", "data.json");

        await WriteTextAsync(path, "one");

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Write_OverwritesLeftoverTempFile()
    {
        // A crash during an earlier save left a half-written .tmp behind.
        var path = _folder.File("data.json");
        await File.WriteAllTextAsync(AtomicFile.TempPath(path), "half-writ");

        await WriteTextAsync(path, "one");

        Assert.Equal("one", await File.ReadAllTextAsync(path));
        Assert.False(File.Exists(AtomicFile.TempPath(path)));
    }

    [Fact]
    public async Task FailedWrite_LeavesOriginalUntouched()
    {
        // Simulates a crash mid-save: the writer throws halfway through.
        var path = _folder.File("data.json");
        await WriteTextAsync(path, "original");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AtomicFile.WriteAsync(path, async (stream, token) =>
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes("half"), token);
                throw new InvalidOperationException("crash");
            }));

        Assert.Equal("original", await File.ReadAllTextAsync(path));
    }
}
