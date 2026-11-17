namespace Ledge.ViewModels;

/// <summary>What's wrong with the shortcut just pressed in Settings (DESIGN.md §5 "Settings window").</summary>
public enum ShortcutProblem
{
    None,

    /// <summary>Doesn't include Win or Ctrl+Alt: "Use Win or Ctrl+Alt together with a key."</summary>
    Invalid,

    /// <summary>Another app owns it: "{hotkey} is already used by another app."</summary>
    Taken,
}
