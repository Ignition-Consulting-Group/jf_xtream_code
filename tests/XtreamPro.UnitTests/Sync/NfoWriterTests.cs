using Jellyfin.Plugin.XtreamPro.Sync;

namespace XtreamPro.UnitTests.Sync;

public class NfoWriterTests
{
    private static readonly SeriesEntry Series = new()
    {
        SeriesId = 301,
        Title = "Fake Show",
        Year = 2019,
        TmdbId = "555",
        Plot = "A fake show & more.",
        Rating = 8,
        Poster = "http://fake.xtream.test/covers/301.jpg",
        Backdrop = "http://fake.xtream.test/backdrops/301.jpg",
        Genres = ["Comedy", "Drama"],
    };

    [Fact]
    public void Movie()
    {
        var movie = new MovieEntry
        {
            StreamId = 201,
            Title = "Fake Movie",
            Year = 2020,
            Extension = "mkv",
            TmdbId = "12345",
            Rating = 7.4,
            Poster = "http://fake.xtream.test/posters/201.jpg",
            Genres = ["Drama", "Thriller"],
        };

        NfoWriter.Movie(movie).ShouldMatchApproved(c => c.WithFileExtension("nfo").SubFolder("Approved"));
    }

    [Fact]
    public void MovieWithoutOptionalFields()
    {
        NfoWriter.Movie(new MovieEntry { StreamId = 1, Title = "Bare", Extension = "mp4" })
            .ShouldMatchApproved(c => c.WithFileExtension("nfo").SubFolder("Approved"));
    }

    [Fact]
    public void MovieInCategoryCollection()
    {
        NfoWriter.Movie(new MovieEntry { StreamId = 1, Title = "Bare", Extension = "mp4", Category = "Action" }, categoryCollection: true)
            .ShouldMatchApproved(c => c.WithFileExtension("nfo").SubFolder("Approved"));
    }

    [Fact]
    public void TvShow()
    {
        NfoWriter.TvShow(Series).ShouldMatchApproved(c => c.WithFileExtension("nfo").SubFolder("Approved"));
    }

    [Fact]
    public void Episode()
    {
        var episode = new EpisodeEntry
        {
            Id = 4001,
            Season = 1,
            Episode = 1,
            Title = "Pilot",
            Extension = "mkv",
            Plot = "Pilot plot.",
            AirDate = "2019-01-01",
            Thumb = "http://fake.xtream.test/eps/4001.jpg",
            TmdbId = "9001",
            Rating = 7.9,
        };

        NfoWriter.Episode(Series, episode).ShouldMatchApproved(c => c.WithFileExtension("nfo").SubFolder("Approved"));
    }
}
