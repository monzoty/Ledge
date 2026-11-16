namespace Ledge.Core.Tests;

/// <summary>
/// A fresh, empty folder under %TEMP% for one test, deleted afterwards.
/// Storage tests never touch the real %LOCALAPPDATA%\Ledge (CLAUDE.md).
/// </summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Ledge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public string[] FileNames() =>
        [.. Directory.GetFiles(Path).Select(System.IO.Path.GetFileName).OfType<string>().Order()];

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
