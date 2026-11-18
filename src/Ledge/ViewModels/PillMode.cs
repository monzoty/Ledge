namespace Ledge.ViewModels;

/// <summary>What the collapsed pill shows (PRD F1.4, DESIGN.md §2).</summary>
public enum PillMode
{
    /// <summary>"✓ All clear": no open tasks today, no timer.</summary>
    AllClear,

    /// <summary>"• 3 today": open tasks today, no timer.</summary>
    TodayCount,

    /// <summary>Ring (or pause icon) + "18:42": a session is running or paused.</summary>
    Timer,
}
