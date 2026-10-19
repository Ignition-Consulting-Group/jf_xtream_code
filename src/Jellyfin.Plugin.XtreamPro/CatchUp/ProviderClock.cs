using System;

namespace Jellyfin.Plugin.XtreamPro.CatchUp;

/// <summary>
/// Converts times to the provider's clock, which timeshift URLs are expressed in.
/// </summary>
public static class ProviderClock
{
    /// <summary>
    /// Converts a time to the provider's local time.
    /// </summary>
    /// <param name="time">The time.</param>
    /// <param name="timezone">IANA zone from <c>server_info.timezone</c>.</param>
    /// <returns>Provider local time, or UTC if the zone is unknown.</returns>
    public static DateTime ToProviderTime(DateTimeOffset time, string? timezone)
    {
        var zone = !string.IsNullOrWhiteSpace(timezone) && TimeZoneInfo.TryFindSystemTimeZoneById(timezone, out var found) ? found : TimeZoneInfo.Utc;
        return TimeZoneInfo.ConvertTime(time, zone).DateTime;
    }
}
