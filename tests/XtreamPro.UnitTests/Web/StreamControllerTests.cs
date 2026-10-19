using System;
using Jellyfin.Plugin.XtreamPro.Sync;
using Jellyfin.Plugin.XtreamPro.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace XtreamPro.UnitTests.Web;

public class StreamControllerTests
{
    private readonly FakeProvider _provider = new();
    private readonly StreamController _controller;
    private readonly StreamUrls _urls;

    public StreamControllerTests()
    {
        _provider.Configuration.StreamSecret = StreamUrls.NewSecret();
        _urls = new StreamUrls("http://jf", _provider.Configuration.StreamSecret);
        _controller = new StreamController(_provider.Settings, _provider.Clients);
    }

    private string Sig(StreamKind kind, int id) => new Uri(_urls.For(_provider.Profile.Id, kind, id, "mkv")).Query["?sig=".Length..];

    [Theory]
    [InlineData("movie", StreamKind.Movie, "http://fake.xtream.test/movie/demo/demo/201.mkv")]
    [InlineData("episode", StreamKind.Episode, "http://fake.xtream.test/series/demo/demo/201.mkv")]
    public void Get_RedirectsToTheProvider(string kind, StreamKind streamKind, string expected)
    {
        var result = _controller.Get(_provider.Profile.Id.ToString("N"), kind, "201.mkv", Sig(streamKind, 201));

        result.ShouldBeOfType<RedirectResult>().Url.ShouldBe(expected);
    }

    [Fact]
    public void Get_WithBadSignature_IsForbidden()
    {
        _controller.Get(_provider.Profile.Id.ToString("N"), "movie", "202.mkv", Sig(StreamKind.Movie, 201))
            .ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Theory]
    [InlineData("not-a-guid", "movie", "1.mkv")]
    [InlineData("99999999222233334444555555555555", "movie", "1.mkv")]
    [InlineData("11111111222233334444555555555555", "live", "1.ts")]
    [InlineData("11111111222233334444555555555555", "movie", "abc.mkv")]
    public void Get_UnknownTargets_AreNotFound(string provider, string kind, string file)
    {
        _controller.Get(provider, kind, file, "x").ShouldBeOfType<NotFoundResult>();
    }
}
