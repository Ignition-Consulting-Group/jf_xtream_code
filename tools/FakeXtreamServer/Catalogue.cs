using System.Globalization;
using Microsoft.Extensions.Options;

namespace FakeXtreamServer;

public sealed record FakeChannel(int StreamId, int Num, string Name, string CategoryId, string? EpgId, int ArchiveDays);

public sealed record FakeMovie(int StreamId, string Name, int Year, string CategoryId, string Extension, string? Tmdb);

public sealed record FakeSeries(int SeriesId, string Name, int Year, string CategoryId);

public class Catalogue
{
    private static readonly string[] CategoryNames = ["UK | News", "Sports", "Kids"];

    public Catalogue(IOptions<FakeProviderOptions> options)
    {
        Options = options.Value;
        Categories = CategoryNames.Select((name, i) => (Id: (i + 1).ToString(CultureInfo.InvariantCulture), Name: name)).ToList();
        Channels = Enumerable.Range(0, Options.LiveChannels)
            .Select(i => new FakeChannel(
                1000 + i,
                i + 1,
                $"Fake Channel {i + 1}",
                Categories[i % Categories.Count].Id,
                i % 5 == 4 ? null : $"ch{i + 1}.fake",
                i % 2 == 0 ? 3 : 0))
            .ToList();
    }

    public FakeProviderOptions Options { get; }

    public IReadOnlyList<(string Id, string Name)> VodCategories { get; } = [("10", "EN | Drama"), ("11", "Comedy"), ("12", "XXX | Adult")];

    public IReadOnlyList<(string Id, string Name)> SeriesCategories { get; } = [("20", "EN | Shows")];

    public IReadOnlyList<FakeMovie> Movies => Enumerable.Range(0, Options.Movies)
        .Select(i => new FakeMovie(
            5000 + i,
            $"EN | Fake Movie {i + 1} ({2000 + (i % 25)})",
            2000 + (i % 25),
            VodCategories[i % VodCategories.Count].Id,
            i % 2 == 0 ? "mkv" : "mp4",
            i % 3 == 0 ? null : (100000 + i).ToString(CultureInfo.InvariantCulture)))
        .ToList();

    public IReadOnlyList<FakeSeries> Series => Enumerable.Range(0, Options.Series)
        .Select(i => new FakeSeries(7000 + i, $"EN | Fake Show {i + 1}", 2010 + i, SeriesCategories[0].Id))
        .ToList();

    public int EpisodeId(FakeSeries series, int season, int episode) => (series.SeriesId * 1000) + (season * 100) + episode;

    public IReadOnlyList<(string Id, string Name)> Categories { get; }

    public IReadOnlyList<FakeChannel> Channels { get; }

    public bool IsMedia(int id)
        => Movies.Any(m => m.StreamId == id) || Series.Any(s => Enumerable.Range(1, Options.Seasons).Any(se => Enumerable.Range(1, Options.EpisodesPerSeason).Any(e => EpisodeId(s, se, e) == id)));

    public bool IsValid(string? username, string? password)
        => username == Options.Username && password == Options.Password;

    public object Number(long value)
        => Options.StringNumbers ? value.ToString(CultureInfo.InvariantCulture) : value;
}
