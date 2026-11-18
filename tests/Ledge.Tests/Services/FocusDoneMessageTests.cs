using System.Globalization;
using Ledge.Core.Models;
using Ledge.Services;

namespace Ledge.Tests.Services;

public sealed class FocusDoneMessageTests
{
    private static readonly FocusDoneTexts Texts = new(
        "Focus session done",
        "{0} min on “{1}”. Time for a short break.",
        "{0} minutes of focus. Time for a short break.",
        "Focus session ended",
        "It ended at {0}, while your PC was asleep.");

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-GB"); // 24-hour "HH:mm"
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 11, 7, 0, TimeSpan.FromHours(5.5));

    private static FocusFinishedEventArgs Finished(int minutes, TimeSpan lateBy) => new(
        new FocusSession { StartedAt = Now - TimeSpan.FromMinutes(minutes) - lateBy, Duration = TimeSpan.FromMinutes(minutes) },
        lateBy);

    [Fact]
    public void Linked_NamesTheTask()
    {
        var message = FocusDoneMessage.Create(Finished(25, TimeSpan.Zero), "Email Priya", Now, Texts, Culture);

        Assert.Equal("Focus session done", message.Title);
        Assert.Equal("25 min on “Email Priya”. Time for a short break.", message.Body);
    }

    [Fact]
    public void Unlinked_JustCountsMinutes()
    {
        var message = FocusDoneMessage.Create(Finished(50, TimeSpan.Zero), null, Now, Texts, Culture);

        Assert.Equal("50 minutes of focus. Time for a short break.", message.Body);
    }

    [Fact]
    public void SlightlyLate_IsStillDone()
    {
        var message = FocusDoneMessage.Create(Finished(15, TimeSpan.FromSeconds(40)), null, Now, Texts, Culture);

        Assert.Equal("Focus session done", message.Title);
    }

    [Fact]
    public void NoticedLongAfter_SaysWhenItEnded()
    {
        // Ended 25 minutes ago (10:42) while the PC slept; noticed now (11:07).
        var message = FocusDoneMessage.Create(Finished(25, TimeSpan.FromMinutes(25)), "Email Priya", Now, Texts, Culture);

        Assert.Equal("Focus session ended", message.Title);
        Assert.Equal("It ended at 10:42, while your PC was asleep.", message.Body);
    }

    [Fact]
    public void EndedTime_FollowsTheUsersClockFormat()
    {
        var message = FocusDoneMessage.Create(
            Finished(25, TimeSpan.FromMinutes(25)), null, Now, Texts, CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal("It ended at 10:42 AM, while your PC was asleep.", message.Body);
    }
}
