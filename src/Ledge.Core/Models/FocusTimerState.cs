namespace Ledge.Core.Models;

/// <summary>What the focus timer is doing.</summary>
public enum FocusTimerState
{
    /// <summary>No session (never started, stopped, or finished).</summary>
    Idle,

    Running,

    Paused,
}
