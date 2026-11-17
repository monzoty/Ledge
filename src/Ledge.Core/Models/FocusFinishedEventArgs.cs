namespace Ledge.Core.Models;

/// <summary>A focus session reached zero (PRD F4.4–F4.5).</summary>
public sealed class FocusFinishedEventArgs : EventArgs
{
    public FocusFinishedEventArgs(FocusSession session, TimeSpan lateBy)
    {
        Session = session;
        LateBy = lateBy;
    }

    /// <summary>The session that ended, including its linked task, if any.</summary>
    public FocusSession Session { get; }

    /// <summary>
    /// How long after its end time this was noticed. About zero normally; large
    /// if the PC was asleep when the time ran out, so the UI can say it ended
    /// while you were away (PRD F4.5).
    /// </summary>
    public TimeSpan LateBy { get; }
}
