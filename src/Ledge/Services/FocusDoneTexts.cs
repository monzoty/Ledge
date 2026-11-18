namespace Ledge.Services;

/// <summary>The notification copy from Strings.xaml (DESIGN.md §6), passed in so the message logic is testable.</summary>
/// <param name="DoneTitle">"Focus session done".</param>
/// <param name="DoneLinked">"{0} min on “{1}”. Time for a short break."</param>
/// <param name="DoneUnlinked">"{0} minutes of focus. Time for a short break."</param>
/// <param name="EndedTitle">"Focus session ended".</param>
/// <param name="EndedBody">"It ended at {0}, while your PC was asleep."</param>
public sealed record FocusDoneTexts(
    string DoneTitle,
    string DoneLinked,
    string DoneUnlinked,
    string EndedTitle,
    string EndedBody);
