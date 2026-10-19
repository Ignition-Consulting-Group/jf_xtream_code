using System;
using Jellyfin.Plugin.XtreamPro.LiveTv;

namespace XtreamPro.UnitTests.LiveTv;

public class ChannelIdTests
{
    [Fact]
    public void Format_RoundTrips()
    {
        var provider = Guid.NewGuid();

        ChannelId.TryParse(ChannelId.Format(provider, 42), out var parsed).ShouldBeTrue();

        parsed.ShouldBe((provider, 42));
    }

    [Theory]
    [InlineData("m3u_abc")]
    [InlineData("xtreampro_notaguid_1")]
    [InlineData("xtreampro_11111111222233334444555555555555_x")]
    [InlineData("")]
    public void TryParse_RejectsOtherIds(string id)
    {
        ChannelId.TryParse(id, out _).ShouldBeFalse();
    }
}
