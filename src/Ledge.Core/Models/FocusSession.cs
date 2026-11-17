namespace Ledge.Core.Models;

/// <summary>
/// One focus session, stored as timestamps rather than a countdown (ADR-0004).
/// Immutable: pausing or resuming makes a new copy with <c>with { … }</c>.
/// Kept in memory only; v0.1 doesn't keep a timer across restarts (PRD F4).
/// </summary>
public sealed record FocusSession
{
    public required DateTimeOffset StartedAt { get; init; }

    public required TimeSpan Duration { get; init; }

    /// <summary>Total time spent paused in earlier pauses (not counting a pause still going on).</summary>
    public TimeSpan PausedTotal { get; init; }

    /// <summary>When the current pause began; null while running.</summary>
    public DateTimeOffset? PausedAt { get; init; }

    /// <summary>The task this session is for, if it was started from a task (PRD F4.1).</summary>
    public Guid? TaskId { get; init; }

    public bool IsPaused => PausedAt is not null;

    /// <summary>When it will end if nothing changes. Only meaningful while running.</summary>
    public DateTimeOffset EndsAt => StartedAt + PausedTotal + Duration;

    /// <summary>
    /// Time left at <paramref name="now"/>: Duration − (now − StartedAt − PausedTotal).
    /// While paused, the clock is frozen at PausedAt. Never negative.
    /// </summary>
    public TimeSpan RemainingAt(DateTimeOffset now)
    {
        var clock = PausedAt ?? now;
        var focused = clock - StartedAt - PausedTotal;
        var remaining = Duration - focused;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
}
