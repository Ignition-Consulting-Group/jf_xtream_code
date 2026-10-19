using System;
using Jellyfin.Plugin.XtreamPro.CatchUp;

namespace XtreamPro.UnitTests.CatchUp;

public class ProviderClockTests
{
    private static readonly DateTimeOffset Utc = new(2026, 7, 4, 7, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToProviderTime_UsesTheServerTimezone()
    {
        ProviderClock.ToProviderTime(Utc, "Europe/London").ShouldBe(new DateTime(2026, 7, 4, 8, 0, 0));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Not/AZone")]
    public void ToProviderTime_UnknownTimezone_FallsBackToUtc(string? zone)
    {
        ProviderClock.ToProviderTime(Utc, zone).ShouldBe(new DateTime(2026, 7, 4, 7, 0, 0));
    }
}
