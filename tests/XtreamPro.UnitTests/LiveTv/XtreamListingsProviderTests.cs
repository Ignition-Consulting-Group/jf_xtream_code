using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.LiveTv;
using Jellyfin.Plugin.XtreamPro.LiveTv.Xmltv;
using MediaBrowser.Model.LiveTv;

namespace XtreamPro.UnitTests.LiveTv;

public class XtreamListingsProviderTests
{
    private readonly LiveTvHarness _h = new();
    private readonly XtreamListingsProvider _listings;

    public XtreamListingsProviderTests()
    {
        _listings = new XtreamListingsProvider(_h.Catalog, _h.Guides);
    }

    [Fact]
    public async Task GetChannels_UsesTheTunerChannelIdsSoJellyfinMatchesThemDirectly()
    {
        var tunerIds = (await _h.Catalog.GetChannelsAsync(false, TestContext.Current.CancellationToken)).Select(c => c.Id);

        var guideChannels = await _listings.GetChannels(new ListingsProviderInfo(), TestContext.Current.CancellationToken);

        guideChannels.Select(c => c.Id).ShouldBe(tunerIds);
    }

    [Fact]
    public async Task GetPrograms_ReadsTheStoredGuide()
    {
        var channel = _h.Channel(101);
        var start = new DateTimeOffset(2026, 3, 4, 7, 0, 0, TimeSpan.Zero);
        await _h.Guides.SaveAsync(
            _h.Provider.Profile.Id,
            new GuideResult(
                new Dictionary<string, IReadOnlyList<XmltvProgramme>> { [channel] = [new XmltvProgramme { ChannelId = "x", Start = start, Stop = start.AddHours(1), Title = "News" }] },
                new Dictionary<string, string>(),
                []),
            TestContext.Current.CancellationToken);

        var programs = await _listings.GetProgramsAsync(new ListingsProviderInfo(), channel, start.UtcDateTime, start.AddDays(1).UtcDateTime, TestContext.Current.CancellationToken);

        programs.ShouldHaveSingleItem().Name.ShouldBe("News");
    }

    [Fact]
    public async Task GetPrograms_ForForeignChannel_IsEmpty()
    {
        (await _listings.GetProgramsAsync(new ListingsProviderInfo(), "m3u_1", DateTime.UtcNow, DateTime.UtcNow.AddDays(1), TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public void Type_MatchesTheTunerHost()
    {
        _listings.Type.ShouldBe(ChannelId.TunerHostId);
    }
}
