namespace Ledge.Core.Models;

/// <summary>
/// Everything saved to settings.json (PRD F6, ARCHITECTURE.md §4). Immutable:
/// a change is a new copy made with <c>with { … }</c>.
/// </summary>
/// <remarks>
/// The focus-timer default ("DefaultFocusMinutes" in ARCHITECTURE.md) is added
/// in M5 together with the timer; an older settings.json without it will
/// simply get the default then.
/// </remarks>
public sealed record AppSettings
{
    /// <summary>The file format version written by this build.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>DESIGN.md §5. Text form; the app turns it into a real key combination.</summary>
    public const string DefaultHotkey = "Win+Alt+L";

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public string Hotkey { get; init; } = DefaultHotkey;

    /// <summary>PRD F5.3: off by default; the user turns it on in Settings.</summary>
    public bool StartWithWindows { get; init; }

    public ThemeMode Theme { get; init; } = ThemeMode.System;
}
