using Microsoft.Win32;

namespace Ledge.Services;

/// <summary>
/// "Start with Windows" (PRD F5.3) for an app that isn't installed through the
/// Store: a value named "Ledge" under the current user's Run key, holding the
/// path of Ledge.exe. Windows starts everything listed there at sign-in.
/// </summary>
/// <remarks>
/// The registry is Windows' settings database. HKEY_CURRENT_USER (HKCU) belongs
/// to the signed-in user, so no administrator rights are needed.
/// You can look at it yourself: regedit → HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
/// (or Task Manager → Startup apps, which lists the same entries).
/// https://learn.microsoft.com/windows/win32/setupapi/run-and-runonce-registry-keys
/// https://learn.microsoft.com/dotnet/api/microsoft.win32.registry.currentuser
/// </remarks>
public sealed class StartupService : IStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Ledge";

    /// <inheritdoc />
    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string;
        }
    }

    /// <summary>
    /// Adds or removes the Run entry. Adding always rewrites the path, so it
    /// stays correct if Ledge.exe was moved (e.g. a new build folder).
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            // Quoted, because the path may contain spaces.
            key.SetValue(ValueName, $"\"{ExePath}\"", RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    /// <summary>The running Ledge.exe (not dotnet.exe, even under `dotnet run`).</summary>
    private static string ExePath => Environment.ProcessPath
        ?? throw new InvalidOperationException("The path of the running Ledge.exe is unknown.");
}
