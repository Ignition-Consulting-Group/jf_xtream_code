using System;
using Jellyfin.Plugin.XtreamPro.Api;

namespace XtreamPro.UnitTests.Api;

public class XtreamUrlBuilderTests
{
    private readonly XtreamUrlBuilder _urls = new(new XtreamCredentials(new Uri("http://fake.xtream.test:8080"), "user", "p@ss word"));

    [Fact]
    public void PlayerApi_WithoutAction_IsTheAccountInfoCall()
    {
        _urls.PlayerApi().ShouldBe(new Uri("http://fake.xtream.test:8080/player_api.php?username=user&password=p%40ss%20word"));
    }

    [Fact]
    public void PlayerApi_AppendsActionAndExtraParameters()
    {
        _urls.PlayerApi("get_live_streams", ("category_id", "12"))
            .ShouldBe(new Uri("http://fake.xtream.test:8080/player_api.php?username=user&password=p%40ss%20word&action=get_live_streams&category_id=12"));
    }

    [Fact]
    public void Xmltv_PointsAtXmltvPhp()
    {
        _urls.Xmltv().ShouldBe(new Uri("http://fake.xtream.test:8080/xmltv.php?username=user&password=p%40ss%20word"));
    }

    [Theory]
    [InlineData(LiveFormat.Ts, "http://fake.xtream.test:8080/live/user/p%40ss%20word/42.ts")]
    [InlineData(LiveFormat.Hls, "http://fake.xtream.test:8080/live/user/p%40ss%20word/42.m3u8")]
    public void Live_UsesTheChosenFormat(LiveFormat format, string expected)
    {
        _urls.Live(42, format).ShouldBe(new Uri(expected));
    }

    [Fact]
    public void Movie_UsesContainerExtension()
    {
        _urls.Movie(7, "mkv").ShouldBe(new Uri("http://fake.xtream.test:8080/movie/user/p%40ss%20word/7.mkv"));
    }

    [Fact]
    public void Episode_UsesSeriesPath()
    {
        _urls.Episode(99, "mp4").ShouldBe(new Uri("http://fake.xtream.test:8080/series/user/p%40ss%20word/99.mp4"));
    }

    [Fact]
    public void Timeshift_FormatsStartAndDurationInMinutes()
    {
        var start = new DateTime(2026, 3, 4, 19, 5, 0, DateTimeKind.Unspecified);

        _urls.Timeshift(42, start, TimeSpan.FromMinutes(90))
            .ShouldBe(new Uri("http://fake.xtream.test:8080/timeshift/user/p%40ss%20word/90/2026-03-04:19-05/42.ts"));
    }

    [Fact]
    public void BaseUrlWithTrailingPath_IsKept()
    {
        var urls = new XtreamUrlBuilder(new XtreamCredentials(new Uri("https://fake.xtream.test/iptv/"), "u", "p"));

        urls.Movie(1, "mp4").ShouldBe(new Uri("https://fake.xtream.test/iptv/movie/u/p/1.mp4"));
    }
}
