using Ledge.Core.Models;

namespace Ledge.Core.Services;

/// <summary>Turns the Theme setting into "dark or light" (PRD F6, DESIGN.md §3).</summary>
public static class ThemeResolver
{
    /// <param name="setting">What the user picked in Settings.</param>
    /// <param name="windowsAppsUseLightTheme">
    /// Windows' own app mode (Settings → Personalization → Colors), only used
    /// for <see cref="ThemeMode.System"/>.
    /// </param>
    public static bool IsDark(ThemeMode setting, bool windowsAppsUseLightTheme) => setting switch
    {
        ThemeMode.Dark => true,
        ThemeMode.Light => false,
        _ => !windowsAppsUseLightTheme,
    };
}
