namespace Gaia.Core.History;

/// <summary>
/// "This month" for the monthly count is the calendar month in Israel time (assumption A7), so a run at
/// 00:30 on the 1st counts toward the new month even though UTC is still in the old one (R9.AC4).
/// </summary>
public static class IsraelMonth
{
    // IANA id; .NET resolves it on Windows too. Includes Israel's daylight-saving rules (UTC+2 / UTC+3).
    private static readonly TimeZoneInfo Israel = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");

    /// <summary>The UTC instant at which the current Israel calendar month began (the 1st, 00:00 Israel time).</summary>
    public static DateTime StartUtc(DateTime utcNow)
    {
        var israelNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), Israel);
        var israelFirst = new DateTime(israelNow.Year, israelNow.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        // Converted with the offset in force on the 1st itself, not today's (daylight saving may differ).
        return TimeZoneInfo.ConvertTimeToUtc(israelFirst, Israel);
    }

    public static DateTime StartUtc(TimeProvider time) => StartUtc(time.GetUtcNow().UtcDateTime);
}
