using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Api;

namespace XtreamPro.UnitTests.Api;

public class XtreamClientTests
{
    private static readonly XtreamCredentials Credentials = new(new Uri("http://fake.xtream.test"), "demo", "secret");

    private static XtreamClient Client(StubHandler handler, string userAgent = "TestAgent/1.0")
        => new(new HttpClient(handler), Credentials, userAgent);

    [Fact]
    public async Task GetAccountInfo_CallsPlayerApiWithoutAction()
    {
        var handler = StubHandler.ForFixture("account_info.json");

        var account = await Client(handler).GetAccountInfoAsync(TestContext.Current.CancellationToken);

        account.UserInfo.MaxConnections.ShouldBe(2);
        handler.Requests.Single().RequestUri.ShouldBe(new Uri("http://fake.xtream.test/player_api.php?username=demo&password=secret"));
    }

    [Fact]
    public async Task Requests_CarryTheConfiguredUserAgent()
    {
        var handler = StubHandler.ForFixture("account_info.json");

        await Client(handler, "MyPlayer/2.0").GetAccountInfoAsync(TestContext.Current.CancellationToken);

        handler.Requests.Single().Headers.UserAgent.ToString().ShouldBe("MyPlayer/2.0");
    }

    [Fact]
    public async Task GetLiveChannels_FiltersByCategoryWhenGiven()
    {
        var handler = StubHandler.ForFixture("live_streams.json");

        var channels = await Client(handler).GetLiveChannelsAsync("2", TestContext.Current.CancellationToken);

        channels.Count.ShouldBe(3);
        handler.Requests.Single().RequestUri!.Query.ShouldEndWith("&action=get_live_streams&category_id=2");
    }

    [Theory]
    [InlineData("live", "get_live_categories")]
    [InlineData("vod", "get_vod_categories")]
    [InlineData("series", "get_series_categories")]
    public async Task GetCategories_UsesTheActionForTheType(string type, string action)
    {
        var handler = StubHandler.ForFixture("live_categories.json");
        var client = Client(handler);
        var ct = TestContext.Current.CancellationToken;

        var categories = type switch
        {
            "live" => await client.GetLiveCategoriesAsync(ct),
            "vod" => await client.GetVodCategoriesAsync(ct),
            _ => await client.GetSeriesCategoriesAsync(ct),
        };

        categories.Count.ShouldBe(3);
        handler.Requests.Single().RequestUri!.Query.ShouldEndWith("&action=" + action);
    }

    [Fact]
    public async Task GetVodItems_WithoutCategory_FetchesAll()
    {
        var handler = StubHandler.ForFixture("vod_streams.json");

        var items = await Client(handler).GetVodItemsAsync(null, TestContext.Current.CancellationToken);

        items.Count.ShouldBe(3);
        handler.Requests.Single().RequestUri!.Query.ShouldEndWith("&action=get_vod_streams");
    }

    [Fact]
    public async Task GetVodInfo_PassesVodId()
    {
        var handler = StubHandler.ForFixture("vod_info.json");

        var info = await Client(handler).GetVodInfoAsync(201, TestContext.Current.CancellationToken);

        info.Info!.TmdbId.ShouldBe("12345");
        handler.Requests.Single().RequestUri!.Query.ShouldEndWith("&action=get_vod_info&vod_id=201");
    }

    [Fact]
    public async Task GetSeries_And_GetSeriesInfo()
    {
        var handler = new StubHandler(r => StubHandler.Json(Fixture.Read(
            r.RequestUri!.Query.Contains("get_series_info", StringComparison.Ordinal) ? "series_info.json" : "series.json")));
        var client = Client(handler);
        var ct = TestContext.Current.CancellationToken;

        (await client.GetSeriesAsync(null, ct)).Count.ShouldBe(2);
        (await client.GetSeriesInfoAsync(301, ct)).Episodes[1].Count.ShouldBe(2);
        handler.Requests[1].RequestUri!.Query.ShouldEndWith("&action=get_series_info&series_id=301");
    }

    [Fact]
    public async Task GetShortEpg_PassesStreamIdAndLimit()
    {
        var handler = StubHandler.ForFixture("short_epg.json");

        var epg = await Client(handler).GetShortEpgAsync(101, 4, TestContext.Current.CancellationToken);

        epg.EpgListings.Count.ShouldBe(2);
        handler.Requests.Single().RequestUri!.Query.ShouldEndWith("&action=get_short_epg&stream_id=101&limit=4");
    }

    [Fact]
    public async Task OpenXmltv_ReturnsTheRawStream()
    {
        var handler = new StubHandler(_ => StubHandler.Json("<tv></tv>"));

        await using var stream = await Client(handler).OpenXmltvAsync(TestContext.Current.CancellationToken);
        using var reader = new StreamReader(stream);

        (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).ShouldBe("<tv></tv>");
        handler.Requests.Single().RequestUri!.AbsolutePath.ShouldBe("/xmltv.php");
    }

    [Fact]
    public async Task ServerError_Throws()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway));

        await Should.ThrowAsync<HttpRequestException>(() => Client(handler).GetAccountInfoAsync(TestContext.Current.CancellationToken));
    }
}
