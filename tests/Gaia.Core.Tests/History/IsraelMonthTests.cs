using Gaia.Core.History;

namespace Gaia.Core.Tests.History;

public class IsraelMonthTests
{
    private static DateTime Utc(int year, int month, int day, int hour, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public void R9_AC4_the_1st_at_00_30_Israel_time_belongs_to_the_new_month_in_winter()
    {
        // 31 Jan 22:30 UTC = 1 Feb 00:30 in Israel (UTC+2). Still January in UTC.
        var start = IsraelMonth.StartUtc(Utc(2026, 1, 31, 22, 30));

        Assert.Equal(Utc(2026, 1, 31, 22, 0), start);   // 1 Feb 00:00 Israel time
    }

    [Fact]
    public void R9_AC4_the_1st_at_00_30_Israel_time_belongs_to_the_new_month_in_summer()
    {
        // 30 Jun 21:30 UTC = 1 Jul 00:30 in Israel (UTC+3, daylight saving).
        var start = IsraelMonth.StartUtc(Utc(2026, 6, 30, 21, 30));

        Assert.Equal(Utc(2026, 6, 30, 21, 0), start);   // 1 Jul 00:00 Israel time
    }

    [Fact]
    public void R9_AC4_mid_month_returns_the_start_of_the_current_Israel_month()
    {
        var start = IsraelMonth.StartUtc(Utc(2026, 11, 15, 10));

        Assert.Equal(Utc(2026, 10, 31, 22, 0), start);   // 1 Nov 00:00 Israel time (UTC+2)
    }

    [Fact]
    public void R9_AC4_uses_the_offset_in_force_on_the_1st_not_today()
    {
        // 30 Oct 2026 is winter time (UTC+2), but 1 Oct 2026 was still summer time (UTC+3).
        var start = IsraelMonth.StartUtc(Utc(2026, 10, 30, 12));

        Assert.Equal(Utc(2026, 9, 30, 21, 0), start);
    }

    [Fact]
    public void T42_takes_the_current_time_from_a_TimeProvider()
    {
        var start = IsraelMonth.StartUtc(new FixedTime(Utc(2026, 1, 31, 22, 30)));

        Assert.Equal(Utc(2026, 1, 31, 22, 0), start);
    }
}
