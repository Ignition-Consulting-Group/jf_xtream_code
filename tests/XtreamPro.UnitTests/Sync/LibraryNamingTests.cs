using System.IO;
using Jellyfin.Plugin.XtreamPro.Sync;

namespace XtreamPro.UnitTests.Sync;

public class LibraryNamingTests
{
    [Fact]
    public void Movie_UsesTitleYearAndTmdbTag()
    {
        var movie = new MovieEntry { StreamId = 1, Title = "Fake Movie", Year = 2020, Extension = "mkv", TmdbId = "123" };

        LibraryNaming.MovieFolder(movie).ShouldBe("Fake Movie (2020) [tmdbid-123]");
        LibraryNaming.MovieFile(movie).ShouldBe("Fake Movie (2020)");
    }

    [Fact]
    public void Movie_WithoutYearOrTmdb_IsJustTheTitle()
    {
        var movie = new MovieEntry { StreamId = 1, Title = "Fake", Extension = "mp4" };

        LibraryNaming.MovieFolder(movie).ShouldBe("Fake");
    }

    [Fact]
    public void Episode_FollowsJellyfinNaming()
    {
        var series = new SeriesEntry { SeriesId = 1, Title = "Fake Show", Year = 2019, TmdbId = "555" };
        var episode = new EpisodeEntry { Id = 9, Season = 1, Episode = 2, Title = "Second", Extension = "mkv" };

        LibraryNaming.SeriesFolder(series).ShouldBe("Fake Show (2019) [tmdbid-555]");
        LibraryNaming.SeasonFolder(episode).ShouldBe("Season 01");
        LibraryNaming.EpisodeFile(series, episode).ShouldBe("Fake Show - S01E02 - Second");
    }

    [Fact]
    public void Episode_WithoutTitle_OmitsTheSuffix()
    {
        var series = new SeriesEntry { SeriesId = 1, Title = "Show" };

        LibraryNaming.EpisodeFile(series, new EpisodeEntry { Id = 1, Season = 10, Episode = 105, Extension = "mp4" }).ShouldBe("Show - S10E105");
    }

    [Theory]
    [InlineData("What? / Why: \"Now\" <here>*|", "What Why Now here")]
    [InlineData("Ends with dot.", "Ends with dot")]
    [InlineData("Tab\tand\nnewline", "Tab and newline")]
    public void Sanitize_RemovesCharactersNoFileSystemAccepts(string raw, string expected)
    {
        LibraryNaming.Sanitize(raw).ShouldBe(expected);
    }

    [Fact]
    public void Sanitize_LimitsLength()
    {
        LibraryNaming.Sanitize(new string('a', 300)).Length.ShouldBe(120);
    }

    [Fact]
    public void Sanitize_ResultIsAValidFileName()
    {
        LibraryNaming.Sanitize("a/b\\c").IndexOfAny(Path.GetInvalidFileNameChars()).ShouldBe(-1);
    }
}
