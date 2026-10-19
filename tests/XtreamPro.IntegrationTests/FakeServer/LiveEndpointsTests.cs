using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Api;
using Jellyfin.Plugin.XtreamPro.LiveTv.Xmltv;
using Microsoft.AspNetCore.Mvc.Testing;

namespace XtreamPro.IntegrationTests.FakeServer;

public class LiveEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly XtreamClient _client;

    public LiveEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = new XtreamClient(factory.CreateClient(), new XtreamCredentials(factory.Server.BaseAddress, "demo", "demo"), "Test/1.0");
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LiveCategoriesAndChannels_AreGenerated()
    {
        var categories = await _client.GetLiveCategoriesAsync(Ct);
        var channels = await _client.GetLiveChannelsAsync(null, Ct);

        categories.ShouldNotBeEmpty();
        channels.Count.ShouldBe(6);
        channels.ShouldAllBe(c => categories.Any(cat => cat.CategoryId == c.CategoryId));
        channels.ShouldContain(c => c.TvArchive && c.TvArchiveDuration == 3);
    }

    [Fact]
    public async Task LiveChannels_FilterByCategory()
    {
        var category = (await _client.GetLiveCategoriesAsync(Ct))[0].CategoryId;

        var channels = await _client.GetLiveChannelsAsync(category, Ct);

        channels.ShouldNotBeEmpty();
        channels.ShouldAllBe(c => c.CategoryId == category);
    }

    [Fact]
    public async Task Xmltv_HasProgrammesAroundNowForEveryChannel()
    {
        var channels = await _client.GetLiveChannelsAsync(null, Ct);
        var programmes = new List<XmltvProgramme>();

        await using var stream = await _client.OpenXmltvAsync(Ct);
        await foreach (var p in XmltvReader.ReadAsync(stream, _ => { }, TimeSpan.Zero, Ct))
        {
            programmes.Add(p);
        }

        var now = DateTimeOffset.UtcNow;
        channels.Where(c => c.EpgChannelId is not null)
            .ShouldAllBe(c => programmes.Any(p => p.ChannelId == c.EpgChannelId && p.Start <= now && p.Stop > now));
    }

    [Fact]
    public async Task ShortEpg_ReturnsDecodedListings()
    {
        var channel = (await _client.GetLiveChannelsAsync(null, Ct))[0];

        var epg = await _client.GetShortEpgAsync(channel.StreamId, 4, Ct);

        epg.EpgListings.Count.ShouldBe(4);
        epg.EpgListings[0].Title.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task LiveStream_ServesTransportStream()
    {
        var channel = (await _client.GetLiveChannelsAsync(null, Ct))[0];
        using var http = _factory.CreateClient();

        using var response = await http.GetAsync(_client.Urls.Live(channel.StreamId, LiveFormat.Ts), HttpCompletionOption.ResponseHeadersRead, Ct);
        await using var body = await response.Content.ReadAsStreamAsync(Ct);
        var buffer = new byte[188];
        await body.ReadExactlyAsync(buffer, Ct);

        response.Content.Headers.ContentType!.MediaType.ShouldBe("video/mp2t");
        buffer[0].ShouldBe((byte)0x47);
    }

    [Fact]
    public async Task LiveStream_BeyondMaxConnections_IsRefused()
    {
        var channel = (await _client.GetLiveChannelsAsync(null, Ct))[0];
        using var http = _factory.CreateClient();
        var url = _client.Urls.Live(channel.StreamId, LiveFormat.Ts);

        using var first = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, Ct);
        using var second = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, Ct);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LiveStream_WrongPassword_IsUnauthorized()
    {
        using var http = _factory.CreateClient();

        using var response = await http.GetAsync(new Uri("/live/demo/nope/1000.ts", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
