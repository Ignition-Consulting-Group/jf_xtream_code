using System;
using System.Collections.Generic;
using Jellyfin.Plugin.XtreamPro.Api;
using Jellyfin.Plugin.XtreamPro.Api.Models;

namespace XtreamPro.UnitTests.Api;

public class ParsingTests
{
    private static T Parse<T>(string fixture) => XtreamJson.Deserialize<T>(Fixture.Read(fixture));

    [Fact]
    public void AccountInfo_ReadsNumbersSentAsStrings()
    {
        var account = Parse<AccountInfo>("account_info.json");

        account.UserInfo.Auth.ShouldBeTrue();
        account.UserInfo.Status.ShouldBe("Active");
        account.UserInfo.MaxConnections.ShouldBe(2);
        account.UserInfo.ActiveConnections.ShouldBe(1);
        account.UserInfo.IsTrial.ShouldBeFalse();
        account.UserInfo.ExpiresAt.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1798761600));
        account.UserInfo.AllowedOutputFormats.ShouldBe(["m3u8", "ts", "rtmp"]);
        account.ServerInfo!.Port.ShouldBe(8080);
        account.ServerInfo.Timezone.ShouldBe("Europe/London");
    }

    [Fact]
    public void AccountInfo_TreatsNullAndEmptyAsUnknown()
    {
        var account = Parse<AccountInfo>("account_info_unlimited.json");

        account.UserInfo.Auth.ShouldBeTrue();
        account.UserInfo.ExpiresAt.ShouldBeNull();
        account.UserInfo.MaxConnections.ShouldBeNull();
        account.UserInfo.ActiveConnections.ShouldBe(0);
    }

    [Fact]
    public void AccountInfo_AuthZero_IsNotAuthenticated()
    {
        var account = Parse<AccountInfo>("account_info_auth_failed.json");

        account.UserInfo.Auth.ShouldBeFalse();
        account.ServerInfo.ShouldBeNull();
    }

    [Fact]
    public void Categories_AcceptStringOrNumberIds()
    {
        var categories = Parse<List<Category>>("live_categories.json");

        categories.ShouldSatisfyAllConditions(
            () => categories.Count.ShouldBe(3),
            () => categories[1].CategoryId.ShouldBe("2"),
            () => categories[2].ParentId.ShouldBeNull());
    }

    [Fact]
    public void LiveStreams_ToleratesStringsEmptiesAndCasing()
    {
        var streams = Parse<List<LiveChannel>>("live_streams.json");

        streams[0].ShouldSatisfyAllConditions(
            s => s.StreamId.ShouldBe(101),
            s => s.TvArchive.ShouldBeTrue(),
            s => s.TvArchiveDuration.ShouldBe(7),
            s => s.Added.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1735689600)));
        streams[1].ShouldSatisfyAllConditions(
            s => s.Num.ShouldBe(2),
            s => s.StreamId.ShouldBe(102),
            s => s.StreamIcon.ShouldBeNull(),
            s => s.EpgChannelId.ShouldBeNull(),
            s => s.TvArchive.ShouldBeFalse());
        streams[2].ShouldSatisfyAllConditions(
            s => s.Name.ShouldBe("Odd Casing"),
            s => s.StreamId.ShouldBe(103),
            s => s.TvArchive.ShouldBeFalse(),
            s => s.TvArchiveDuration.ShouldBeNull());
    }

    [Fact]
    public void VodStreams_ReadRatingAndTmdbInAnyShape()
    {
        var streams = Parse<List<VodItem>>("vod_streams.json");

        streams[0].Rating.ShouldBe(7.4);
        streams[0].Tmdb.ShouldBe("12345");
        streams[1].Rating.ShouldBeNull();
        streams[1].Tmdb.ShouldBe("67890");
        streams[1].Added.ShouldBeNull();
        streams[1].CategoryId.ShouldBe("10");
        streams[2].Rating.ShouldBeNull();
        streams[2].Tmdb.ShouldBeNull();
        streams[2].ContainerExtension.ShouldBeNull();
    }

    [Fact]
    public void VodInfo_ReadsDetails()
    {
        var info = Parse<VodInfo>("vod_info.json");

        info.Info.ShouldNotBeNull();
        info.Info.TmdbId.ShouldBe("12345");
        info.Info.ReleaseDate.ShouldBe("2020-05-01");
        info.Info.BackdropPath.ShouldBe(["http://fake.xtream.test/backdrops/201.jpg"]);
        info.Info.DurationSecs.ShouldBe(5400);
        info.MovieData!.StreamId.ShouldBe(201);
    }

    [Fact]
    public void VodInfo_EmptyArrayInsteadOfObject_IsNull()
    {
        var info = Parse<VodInfo>("vod_info_empty.json");

        info.Info.ShouldBeNull();
        info.MovieData!.StreamId.ShouldBe(202);
    }

    [Fact]
    public void Series_BackdropAsStringOrArray()
    {
        var series = Parse<List<Series>>("series.json");

        series[0].BackdropPath.ShouldBe(["http://fake.xtream.test/backdrops/301.jpg"]);
        series[0].ReleaseDate.ShouldBe("2019-01-01");
        series[0].LastModified.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1735689600));
        series[1].SeriesId.ShouldBe(302);
        series[1].BackdropPath.ShouldBeEmpty();
        series[1].ReleaseDate.ShouldBeNull();
    }

    [Fact]
    public void SeriesInfo_EpisodesKeyedBySeason()
    {
        var info = Parse<SeriesInfo>("series_info.json");

        info.Info!.Name.ShouldBe("Fake Show");
        var season = info.Episodes.ShouldHaveSingleItem();
        season.Key.ShouldBe(1);
        season.Value[0].ShouldSatisfyAllConditions(
            e => e.Id.ShouldBe(4001),
            e => e.Info!.TmdbId.ShouldBe("9001"));
        season.Value[1].ShouldSatisfyAllConditions(
            e => e.EpisodeNum.ShouldBe(2),
            e => e.Season.ShouldBe(1),
            e => e.Info.ShouldBeNull());
    }

    [Fact]
    public void SeriesInfo_EpisodesAsArrayOfSeasons_AreGroupedBySeason()
    {
        var info = Parse<SeriesInfo>("series_info_episodes_array.json");

        info.Info.ShouldBeNull();
        info.Episodes.Keys.ShouldBe([1, 2], ignoreOrder: true);
        info.Episodes[2].ShouldHaveSingleItem().Id.ShouldBe(5002);
    }

    [Fact]
    public void ShortEpg_DecodesBase64TitleAndDescription()
    {
        var epg = Parse<ShortEpg>("short_epg.json");

        epg.EpgListings[0].ShouldSatisfyAllConditions(
            l => l.Title.ShouldBe("Morning News"),
            l => l.Description.ShouldBe("The morning bulletin."),
            l => l.Start.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1772607600)),
            l => l.Stop.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1772611200)),
            l => l.HasArchive.ShouldBeTrue());
        epg.EpgListings[1].Title.ShouldBeNull();
        epg.EpgListings[1].HasArchive.ShouldBeFalse();
    }

    [Fact]
    public void List_SentAsEmptyObject_IsEmpty()
    {
        Parse<List<LiveChannel>>("empty_object_for_list.json").ShouldBeEmpty();
    }
}
