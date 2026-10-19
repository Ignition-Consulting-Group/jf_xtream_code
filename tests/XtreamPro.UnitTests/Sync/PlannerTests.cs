using System;
using System.Linq;
using Jellyfin.Plugin.XtreamPro.Api;
using Jellyfin.Plugin.XtreamPro.Api.Models;
using Jellyfin.Plugin.XtreamPro.Configuration;
using Jellyfin.Plugin.XtreamPro.Sync;

namespace XtreamPro.UnitTests.Sync;

public class PlannerTests
{
    private static readonly Category[] Categories =
    [
        new() { CategoryId = "10", CategoryName = "Movies | Drama" },
        new() { CategoryId = "11", CategoryName = "XXX | Adult" },
        new() { CategoryId = "20", CategoryName = "Shows" },
    ];

    private readonly ProviderProfile _profile = new() { Id = Guid.Parse("11111111-2222-3333-4444-555555555555") };
    private readonly Planner _planner = new(new TitleCleaner(TitleCleaner.DefaultRules), new StreamUrls("http://jf:8096", StreamUrls.NewSecret()));

    private static VodItem Vod(int id, string name, string category = "10", string? ext = "mkv", string? tmdb = null)
        => new() { StreamId = id, Name = name, CategoryId = category, ContainerExtension = ext, Tmdb = tmdb };

    [Fact]
    public void Movies_WriteStrmAndNfoInATitleYearFolder()
    {
        var item = _planner.Movies(_profile, Categories, [Vod(201, "EN | Fake Movie (2020)", tmdb: "12345")]).ShouldHaveSingleItem();

        item.Key.ShouldBe("movie:201");
        item.Files.Keys.ShouldBe(
            ["Fake Movie (2020) [tmdbid-12345]/Fake Movie (2020).strm", "Fake Movie (2020) [tmdbid-12345]/movie.nfo"],
            ignoreOrder: true);
        item.Files["Fake Movie (2020) [tmdbid-12345]/Fake Movie (2020).strm"].ShouldStartWith("http://jf:8096/XtreamPro/Stream/11111111222233334444555555555555/movie/201.mkv?sig=");
        item.Files["Fake Movie (2020) [tmdbid-12345]/movie.nfo"].ShouldContain("<title>Fake Movie</title>");
    }

    [Fact]
    public void FolderTag_IsAppendedToTopLevelFolders()
    {
        var planner = new Planner(new TitleCleaner([]), new StreamUrls("http://jf", StreamUrls.NewSecret()), "Second");
        var series = XtreamJson.Deserialize<Series[]>(Fixture.Read("series.json"))[0];

        planner.Movies(_profile, Categories, [Vod(1, "A")]).Single().Files.Keys.ShouldAllBe(k => k.StartsWith("A [Second]/", StringComparison.Ordinal));
        planner.Series(_profile, Categories, series, XtreamJson.Deserialize<SeriesInfo>(Fixture.Read("series_info.json")))
            .Files.Keys.ShouldAllBe(k => k.StartsWith("EN Fake Show (2019) [tmdbid-555] [Second]/", StringComparison.Ordinal));
    }

    [Fact]
    public void Movies_SkipAdultAndExcludedCategories()
    {
        _profile.VodCategories.Add(new CategoryRule { CategoryId = "10", Excluded = true });

        _planner.Movies(_profile, Categories, [Vod(1, "A"), Vod(2, "B", "11"), Vod(3, "C", "99")]).Select(i => i.Key).ShouldBe(["movie:3"]);
    }

    [Fact]
    public void Movies_AdultCategoriesCanBeAllowed()
    {
        _profile.SkipAdultCategories = false;

        _planner.Movies(_profile, Categories, [Vod(2, "B", "11")]).ShouldHaveSingleItem();
    }

    [Fact]
    public void Movies_WithSameName_GetDistinctFolders()
    {
        var items = _planner.Movies(_profile, Categories, [Vod(1, "Same (2020)"), Vod(2, "Same (2020)")]);

        items.SelectMany(i => i.Files.Keys).Distinct(StringComparer.OrdinalIgnoreCase).Count().ShouldBe(4);
    }

    [Fact]
    public void Movies_ListedTwice_ArePlannedOnce()
    {
        _planner.Movies(_profile, Categories, [Vod(1, "A"), Vod(1, "A")]).ShouldHaveSingleItem();
    }

    [Fact]
    public void Movies_MissingExtension_DefaultsToMp4()
    {
        _planner.Movies(_profile, Categories, [Vod(1, "A", ext: null)]).Single().Files.Keys.ShouldContain(k => k.EndsWith(".strm", StringComparison.Ordinal));
        _planner.Movies(_profile, Categories, [Vod(1, "A", ext: null)]).Single().Files.Values.ShouldContain(v => v.Contains("/movie/1.mp4?", StringComparison.Ordinal));
    }

    [Fact]
    public void Series_WriteShowSeasonAndEpisodeFiles()
    {
        var series = XtreamJson.Deserialize<Series[]>(Fixture.Read("series.json"))[0];
        var info = XtreamJson.Deserialize<SeriesInfo>(Fixture.Read("series_info.json"));

        var item = _planner.Series(_profile, Categories, series, info).ShouldNotBeNull();

        item.Key.ShouldBe("series:301");
        item.Files.Keys.ShouldBe(
        [
            "Fake Show (2019) [tmdbid-555]/tvshow.nfo",
            "Fake Show (2019) [tmdbid-555]/Season 01/Fake Show - S01E01 - Pilot.strm",
            "Fake Show (2019) [tmdbid-555]/Season 01/Fake Show - S01E01 - Pilot.nfo",
            "Fake Show (2019) [tmdbid-555]/Season 01/Fake Show - S01E02 - Second.strm",
            "Fake Show (2019) [tmdbid-555]/Season 01/Fake Show - S01E02 - Second.nfo",
        ],
            ignoreOrder: true);
        item.Files["Fake Show (2019) [tmdbid-555]/Season 01/Fake Show - S01E02 - Second.strm"].ShouldContain("/episode/4002.mp4?");
    }

    [Fact]
    public void Series_InExcludedCategory_IsSkipped()
    {
        _profile.SeriesCategories.Add(new CategoryRule { CategoryId = "20", Excluded = true });
        var series = XtreamJson.Deserialize<Series[]>(Fixture.Read("series.json"))[0];

        _planner.IncludeSeries(_profile, Categories, series).ShouldBeFalse();
    }

    [Fact]
    public void EpisodeTitle_DropsTheRepeatedSeriesPrefix()
    {
        Planner.EpisodeTitle("Fake Show - S01E01 - Pilot").ShouldBe("Pilot");
        Planner.EpisodeTitle("Just A Title").ShouldBe("Just A Title");
        Planner.EpisodeTitle("Fake Show - S01E01").ShouldBeNull();
    }
}
