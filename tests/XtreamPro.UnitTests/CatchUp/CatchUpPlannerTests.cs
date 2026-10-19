using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.XtreamPro.Api.Models;
using Jellyfin.Plugin.XtreamPro.CatchUp;
using Jellyfin.Plugin.XtreamPro.Configuration;
using Jellyfin.Plugin.XtreamPro.LiveTv;
using Jellyfin.Plugin.XtreamPro.LiveTv.Xmltv;
using Jellyfin.Plugin.XtreamPro.Sync;

namespace XtreamPro.UnitTests.CatchUp;

public class CatchUpPlannerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly ProviderProfile _profile = new() { Id = Guid.Parse("11111111-2222-3333-4444-555555555555") };
    private readonly CatchUpPlanner _planner = new(new StreamUrls("http://jf", StreamUrls.NewSecret()), TimeZoneInfo.Utc);

    private static XmltvProgramme At(DateTimeOffset start, string title, int minutes = 60)
        => new() { ChannelId = "x", Start = start, Stop = start.AddMinutes(minutes), Title = title, Description = "Plot." };

    private GuideResult Guide(int streamId, params XmltvProgramme[] programmes)
        => new(new Dictionary<string, IReadOnlyList<XmltvProgramme>> { [ChannelId.Format(_profile.Id, streamId)] = programmes }, new Dictionary<string, string>(), []);

    private static LiveChannel Channel(int id, bool archive = true, int days = 3)
        => new() { StreamId = id, Name = "UK: News One", TvArchive = archive, TvArchiveDuration = days, StreamIcon = "http://fake.xtream.test/logo.png" };

    [Fact]
    public void Plan_WritesFinishedProgrammesInsideTheArchiveWindow()
    {
        var guide = Guide(
            101,
            At(Now.AddDays(-4), "Too Old"),
            At(Now.AddDays(-1), "Yesterday"),
            At(Now.AddMinutes(-30), "Still On"),
            At(Now.AddHours(1), "Future"));

        var items = _planner.Plan(_profile, [Channel(101)], guide, Now);

        items.Select(i => i.Key).ShouldBe(["catchup:101:" + Now.AddDays(-1).ToUnixTimeSeconds(), "catchup:101:channel"], ignoreOrder: true);
    }

    [Fact]
    public void Plan_UsesAShowPerChannelAndAnEpisodePerProgramme()
    {
        var start = new DateTimeOffset(2026, 3, 9, 7, 0, 0, TimeSpan.Zero);

        var items = _planner.Plan(_profile, [Channel(101)], Guide(101, At(start, "Morning News")), Now);

        var show = items.Single(i => i.Key.EndsWith(":channel", StringComparison.Ordinal));
        show.Files.Keys.ShouldBe(["UK News One/tvshow.nfo"]);
        var episode = items.Single(i => i != show);
        episode.Files.Keys.ShouldBe(
            ["UK News One/Season 2026/UK News One - 2026-03-09 07.00 - Morning News.strm", "UK News One/Season 2026/UK News One - 2026-03-09 07.00 - Morning News.nfo"],
            ignoreOrder: true);
        episode.Files.Values.First(v => v.StartsWith("http", StringComparison.Ordinal)).ShouldContain("/catchup/101.ts?start=" + start.ToUnixTimeSeconds() + "&minutes=60&sig=");
        var nfo = episode.Files.Values.First(v => v.StartsWith("<?xml", StringComparison.Ordinal));
        nfo.ShouldContain("<season>2026</season>");
        nfo.ShouldContain("<episode>3090700</episode>");
        nfo.ShouldContain("<aired>2026-03-09</aired>");
    }

    [Fact]
    public void Plan_OverlappingGuideEntries_GiveOneEpisode()
    {
        var start = Now.AddHours(-3);

        var items = _planner.Plan(_profile, [Channel(101)], Guide(101, At(start, "A"), At(start, "B")), Now);

        items.Select(i => i.Key).Distinct().Count().ShouldBe(items.Count);
        items.Count.ShouldBe(2);
    }

    [Fact]
    public void Plan_SkipsChannelsWithoutArchive()
    {
        _planner.Plan(_profile, [Channel(101, archive: false)], Guide(101, At(Now.AddHours(-3), "P")), Now).ShouldBeEmpty();
    }

    [Fact]
    public void Plan_ChannelWithNoFinishedProgrammes_HasNoShow()
    {
        _planner.Plan(_profile, [Channel(101)], Guide(101, At(Now.AddHours(1), "Future")), Now).ShouldBeEmpty();
    }

    [Fact]
    public void Plan_NamesUseTheDisplayTimeZone()
    {
        var planner = new CatchUpPlanner(new StreamUrls("http://jf", StreamUrls.NewSecret()), TimeZoneInfo.CreateCustomTimeZone("Plus10", TimeSpan.FromHours(10), "Plus10", "Plus10"));

        var items = planner.Plan(_profile, [Channel(101)], Guide(101, At(new DateTimeOffset(2026, 3, 9, 7, 0, 0, TimeSpan.Zero), "P")), Now);

        items.SelectMany(i => i.Files.Keys).ShouldContain(k => k.Contains("2026-03-09 17.00", StringComparison.Ordinal));
    }
}
