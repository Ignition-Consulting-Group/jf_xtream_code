using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Api;
using Jellyfin.Plugin.XtreamPro.Api.Models;
using Jellyfin.Plugin.XtreamPro.Configuration;
using Jellyfin.Plugin.XtreamPro.LiveTv;
using Microsoft.Extensions.Logging.Abstractions;

namespace XtreamPro.UnitTests.LiveTv;

public class GuideBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 4, 6, 0, 0, TimeSpan.Zero);

    private readonly ProviderProfile _profile = new() { Id = Guid.NewGuid(), Url = "http://fake.xtream.test" };
    private readonly LiveChannel[] _channels = XtreamJson.Deserialize<LiveChannel[]>(Fixture.Read("live_streams.json"));

    private static StubHandler Provider(string xmltvFixture = "guide.xml")
        => new(r => r.RequestUri!.AbsolutePath == "/xmltv.php"
            ? StubHandler.Json(Fixture.Read(xmltvFixture))
            : StubHandler.Json(Fixture.Read("short_epg.json")));

    private Task<GuideResult> Build(StubHandler handler, int shortEpgBudget = 10)
    {
        var client = new XtreamClient(new HttpClient(handler), new XtreamCredentials(new Uri(_profile.Url), "u", "p"), "t");
        return new GuideBuilder(shortEpgBudget, NullLogger.Instance).BuildAsync(_profile, client, _channels, Now, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Build_KeysProgrammesByJellyfinChannel()
    {
        var guide = await Build(Provider());

        var news = guide.Programmes[ChannelId.Format(_profile.Id, 101)];
        news.Select(p => p.Title).ShouldBe(["Morning News & Weather", "With Offset"]);
    }

    [Fact]
    public async Task Build_FillsMissingStopFromTheNextProgramme()
    {
        var guide = await Build(Provider());

        guide.Programmes.Values.SelectMany(p => p).ShouldAllBe(p => p.Stop != null);
    }

    [Fact]
    public async Task Build_UsesShortEpgForChannelsWithoutGuideData()
    {
        var handler = Provider();

        var guide = await Build(handler);

        guide.Programmes[ChannelId.Format(_profile.Id, 102)].First().Title.ShouldBe("Morning News");
        guide.Programmes[ChannelId.Format(_profile.Id, 103)].ShouldNotBeEmpty();
        handler.Requests.Count(r => r.RequestUri!.Query.Contains("get_short_epg", StringComparison.Ordinal)).ShouldBe(2);
    }

    [Fact]
    public async Task Build_StopsShortEpgCallsAtTheBudget()
    {
        var handler = Provider();

        var guide = await Build(handler, shortEpgBudget: 1);

        handler.Requests.Count(r => r.RequestUri!.Query.Contains("get_short_epg", StringComparison.Ordinal)).ShouldBe(1);
        guide.ChannelsWithoutGuide.ShouldBe([ChannelId.Format(_profile.Id, 103)]);
    }

    [Fact]
    public async Task Build_CollectsGuideIcons()
    {
        var guide = await Build(Provider());

        guide.Icons["news.one.uk"].ShouldBe("http://fake.xtream.test/guide/news.png");
    }

    [Fact]
    public async Task Build_WhenXmltvFails_FallsBackToShortEpg()
    {
        var handler = new StubHandler(r => r.RequestUri!.AbsolutePath == "/xmltv.php"
            ? new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            : StubHandler.Json(Fixture.Read("short_epg.json")));

        var guide = await Build(handler);

        guide.Programmes.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Build_DropsProgrammesThatEndedBeforeTheArchiveWindow()
    {
        var guide = await Build(Provider());
        var later = await new GuideBuilder(10, NullLogger.Instance).BuildAsync(
            _profile,
            new XtreamClient(new HttpClient(Provider()), new XtreamCredentials(new Uri(_profile.Url), "u", "p"), "t"),
            _channels.Select(c => c with { TvArchive = false, TvArchiveDuration = null }).ToArray(),
            Now.AddDays(1),
            TestContext.Current.CancellationToken);

        guide.Programmes.ShouldNotBeEmpty();
        later.Programmes.Values.SelectMany(p => p).ShouldBeEmpty();
    }
}
