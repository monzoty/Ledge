using Microsoft.Extensions.Time.Testing;

namespace Ledge.Core.Tests;

/// <summary>
/// Creates FakeTimeProviders for tests. The time zone is UTC+05:30 on
/// purpose: if code mixes up UTC and local dates, tests near midnight fail.
/// </summary>
internal static class TestClock
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(5.5);

    public static readonly TimeZoneInfo Zone =
        TimeZoneInfo.CreateCustomTimeZone("Test+05:30", Offset, "Test+05:30", "Test+05:30");

    /// <summary>16 Nov 2026, 10:00 local time.</summary>
    public static readonly DateTimeOffset DefaultStart = Local(2026, 11, 16, 10, 0);

    public static DateOnly Day16 => new(2026, 11, 16);

    public static FakeTimeProvider Create() => Create(DefaultStart);

    public static FakeTimeProvider Create(DateTimeOffset start)
    {
        // FakeTimeProvider's clock must hold a UTC value, like the real system
        // clock (GetUtcNow). Given "23:59 +05:30" as-is, GetLocalNow would add
        // the offset a second time and land on 05:29 the next day.
        var time = new FakeTimeProvider(start.ToUniversalTime());
        time.SetLocalTimeZone(Zone);
        return time;
    }

    /// <summary>A moment written in the test zone's local time.</summary>
    public static DateTimeOffset Local(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, Offset);
}
