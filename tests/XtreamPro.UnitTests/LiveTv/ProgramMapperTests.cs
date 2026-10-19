using System;
using Jellyfin.Plugin.XtreamPro.LiveTv;
using Jellyfin.Plugin.XtreamPro.LiveTv.Xmltv;

namespace XtreamPro.UnitTests.LiveTv;

public class ProgramMapperTests
{
    private static readonly XmltvProgramme Episode = new()
    {
        ChannelId = "news.one.uk",
        Start = new DateTimeOffset(2026, 3, 4, 8, 0, 0, TimeSpan.FromHours(1)),
        Stop = new DateTimeOffset(2026, 3, 4, 9, 0, 0, TimeSpan.FromHours(1)),
        Title = "Show",
        SubTitle = "Pilot",
        Description = "Plot.",
        Categories = ["Drama"],
        Icon = "http://fake.xtream.test/p.jpg",
        Season = 1,
        Episode = 2,
        Year = 2025,
    };

    [Fact]
    public void ToProgramInfo_MapsFieldsAndConvertsToUtc()
    {
        var info = ProgramMapper.ToProgramInfo("ch1", Episode);

        info.ShouldSatisfyAllConditions(
            i => i.Id.ShouldBe("ch1_1772607600"),
            i => i.ChannelId.ShouldBe("ch1"),
            i => i.Name.ShouldBe("Show"),
            i => i.EpisodeTitle.ShouldBe("Pilot"),
            i => i.Overview.ShouldBe("Plot."),
            i => i.StartDate.ShouldBe(new DateTime(2026, 3, 4, 7, 0, 0, DateTimeKind.Utc)),
            i => i.StartDate.Kind.ShouldBe(DateTimeKind.Utc),
            i => i.EndDate.ShouldBe(new DateTime(2026, 3, 4, 8, 0, 0, DateTimeKind.Utc)),
            i => i.Genres.ShouldBe(["Drama"]),
            i => i.ImageUrl.ShouldBe("http://fake.xtream.test/p.jpg"),
            i => i.HasImage.ShouldBe(true),
            i => i.IsSeries.ShouldBeTrue(),
            i => i.SeasonNumber.ShouldBe(1),
            i => i.EpisodeNumber.ShouldBe(2),
            i => i.ProductionYear.ShouldBe(2025));
    }

    [Theory]
    [InlineData("Movie", true, false, false, false)]
    [InlineData("Sports", false, true, false, false)]
    [InlineData("Football", false, true, false, false)]
    [InlineData("News", false, false, true, false)]
    [InlineData("Children", false, false, false, true)]
    public void ToProgramInfo_FlagsFromCategories(string category, bool movie, bool sports, bool news, bool kids)
    {
        var info = ProgramMapper.ToProgramInfo("ch1", Episode with { Categories = [category], Season = null, Episode = null, SubTitle = null });

        info.IsMovie.ShouldBe(movie);
        info.IsSports.ShouldBe(sports);
        info.IsNews.ShouldBe(news);
        info.IsKids.ShouldBe(kids);
        info.IsSeries.ShouldBeFalse();
    }
}
