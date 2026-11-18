using System.Globalization;
using Ledge.Core.Models;

namespace Ledge.Services;

/// <summary>
/// The notification text for a finished focus session (DESIGN.md §6):
/// "Focus session done" with a linked or unlinked body, or, if the end was
/// noticed long after it happened (the PC was asleep), "Focus session ended"
/// with the time it ended (PRD F4.5).
/// </summary>
/// <param name="Title">The notification's title.</param>
/// <param name="Body">The notification's text.</param>
public sealed record FocusDoneMessage(string Title, string Body)
{
    /// <summary>Noticed more than this late means "it ended while you were away" (DESIGN.md §2).</summary>
    public static readonly TimeSpan AsleepThreshold = TimeSpan.FromMinutes(1);

    /// <param name="finished">What ended, and how late it was noticed.</param>
    /// <param name="taskTitle">The linked task's title, or null (unlinked, or the task was deleted).</param>
    /// <param name="now">The current LOCAL time, to work out when it ended.</param>
    /// <param name="texts">The copy from Strings.xaml.</param>
    /// <param name="culture">For the time format, e.g. "10:42" or "10:42 AM".</param>
    public static FocusDoneMessage Create(
        FocusFinishedEventArgs finished,
        string? taskTitle,
        DateTimeOffset now,
        FocusDoneTexts texts,
        CultureInfo culture)
    {
        if (finished.LateBy > AsleepThreshold)
        {
            var endedAt = (now - finished.LateBy).ToString("t", culture); // short time format
            return new FocusDoneMessage(texts.EndedTitle, string.Format(culture, texts.EndedBody, endedAt));
        }

        var minutes = (int)Math.Round(finished.Session.Duration.TotalMinutes);
        var body = taskTitle is null
            ? string.Format(culture, texts.DoneUnlinked, minutes)
            : string.Format(culture, texts.DoneLinked, minutes, taskTitle);

        return new FocusDoneMessage(texts.DoneTitle, body);
    }
}
