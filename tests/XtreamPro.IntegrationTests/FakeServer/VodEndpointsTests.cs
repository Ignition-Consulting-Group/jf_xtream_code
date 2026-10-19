using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Api;
using Microsoft.AspNetCore.Mvc.Testing;

namespace XtreamPro.IntegrationTests.FakeServer;

public class VodEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly XtreamClient _client;

    public VodEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b => b.UseSetting("Fake:Movies", "25"));
        _client = new XtreamClient(_factory.CreateClient(), new XtreamCredentials(_factory.Server.BaseAddress, "demo", "demo"), "Test/1.0");
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Movies_HonourTheCatalogueSizeAndParseDespiteQuirks()
    {
        var movies = await _client.GetVodItemsAsync(null, Ct);

        movies.Count.ShouldBe(25);
        movies.ShouldContain(m => m.Added == null);
        movies.ShouldContain(m => m.Tmdb == null);
        movies.ShouldContain(m => m.Rating == null);
    }

    [Fact]
    public async Task VodInfo_SometimesSendsAnEmptyArray()
    {
        (await _client.GetVodInfoAsync(5000, Ct)).Info.ShouldNotBeNull();
        (await _client.GetVodInfoAsync(5001, Ct)).Info.ShouldBeNull();
    }

    [Fact]
    public async Task Series_HaveSeasonsOfEpisodes()
    {
        var series = await _client.GetSeriesAsync(null, Ct);
        var info = await _client.GetSeriesInfoAsync(series[0].SeriesId, Ct);

        series.Count.ShouldBe(3);
        info.Episodes.Keys.ShouldBe([1, 2]);
        info.Episodes[1].Count.ShouldBe(3);
        info.Episodes[1][0].Info.ShouldBeNull();
    }

    [Fact]
    public async Task MovieAndEpisodeStreams_AreServed()
    {
        using var http = _factory.CreateClient();
        var episode = (await _client.GetSeriesInfoAsync(7000, Ct)).Episodes[1][0].Id;

        using (var movie = await http.GetAsync(_client.Urls.Movie(5000, "mkv"), HttpCompletionOption.ResponseHeadersRead, Ct))
        {
            movie.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var ep = await http.GetAsync(_client.Urls.Episode(episode, "mkv"), HttpCompletionOption.ResponseHeadersRead, Ct);
        ep.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
