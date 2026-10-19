using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.XtreamPro.Api.Models;
using Jellyfin.Plugin.XtreamPro.Configuration;

namespace Jellyfin.Plugin.XtreamPro.Sync;

/// <summary>
/// Works out which files each movie and series should have in the library.
/// </summary>
public partial class Planner
{
    private const string DefaultExtension = "mp4";

    private readonly TitleCleaner _cleaner;
    private readonly StreamUrls _urls;
    private readonly string _folderSuffix;

    /// <summary>
    /// Initializes a new instance of the <see cref="Planner"/> class.
    /// </summary>
    /// <param name="cleaner">Title cleaner.</param>
    /// <param name="urls">Stream URL builder.</param>
    /// <param name="folderTag">Tag added to every top-level folder, so providers sharing a library never collide.</param>
    public Planner(TitleCleaner cleaner, StreamUrls urls, string? folderTag = null)
    {
        _cleaner = cleaner;
        _urls = urls;
        _folderSuffix = string.IsNullOrWhiteSpace(folderTag) ? string.Empty : $" [{LibraryNaming.Sanitize(folderTag)}]";
    }

    /// <summary>
    /// Plans movie files.
    /// </summary>
    /// <param name="profile">Provider profile.</param>
    /// <param name="categories">VOD categories.</param>
    /// <param name="items">Provider movies.</param>
    /// <returns>One item per movie that passes the category filters.</returns>
    public IReadOnlyList<SyncItem> Movies(ProviderProfile profile, IEnumerable<Category> categories, IEnumerable<VodItem> items)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var names = CategoryNames(categories);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var planned = new List<SyncItem>();
        foreach (var item in items)
        {
            if (!Include(profile, profile.VodCategories, names, item.CategoryId))
            {
                continue;
            }

            var (title, year) = _cleaner.Clean(item.Name ?? item.StreamId.ToString(CultureInfo.InvariantCulture));
            var movie = new MovieEntry
            {
                StreamId = item.StreamId,
                Title = title,
                Year = year,
                Extension = item.ContainerExtension ?? DefaultExtension,
                TmdbId = item.Tmdb,
                Rating = item.Rating,
                Poster = item.StreamIcon,
                Category = item.CategoryId is null ? null : names.GetValueOrDefault(item.CategoryId),
            };

            var folder = Unique(used, LibraryNaming.MovieFolder(movie) + _folderSuffix, item.StreamId);
            planned.Add(new SyncItem(
                "movie:" + item.StreamId.ToString(CultureInfo.InvariantCulture),
                new Dictionary<string, string>
                {
                    [$"{folder}/{LibraryNaming.MovieFile(movie)}.strm"] = _urls.For(profile.Id, StreamKind.Movie, movie.StreamId, movie.Extension),
                    [$"{folder}/movie.nfo"] = NfoWriter.Movie(movie, profile.CategoryCollections),
                }));
        }

        return planned;
    }

    /// <summary>
    /// Checks whether a series passes the category filters, before its episodes are fetched.
    /// </summary>
    /// <param name="profile">Provider profile.</param>
    /// <param name="categories">Series categories.</param>
    /// <param name="series">The series.</param>
    /// <returns>True if the series should be synced.</returns>
    public bool IncludeSeries(ProviderProfile profile, IEnumerable<Category> categories, Series series)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(series);
        return Include(profile, profile.SeriesCategories, CategoryNames(categories), series.CategoryId);
    }

    /// <summary>
    /// Plans the files for one series.
    /// </summary>
    /// <param name="profile">Provider profile.</param>
    /// <param name="categories">Series categories.</param>
    /// <param name="series">The series from <c>get_series</c>.</param>
    /// <param name="info">The series details from <c>get_series_info</c>.</param>
    /// <param name="usedFolders">Folder names already taken by other series.</param>
    /// <returns>The planned files.</returns>
    public SyncItem Series(ProviderProfile profile, IEnumerable<Category> categories, Series series, SeriesInfo info, ISet<string>? usedFolders = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(info);

        var (title, year) = _cleaner.Clean(series.Name ?? info.Info?.Name ?? series.SeriesId.ToString(CultureInfo.InvariantCulture));
        var entry = new SeriesEntry
        {
            SeriesId = series.SeriesId,
            Title = title,
            Year = year ?? ParseYear(series.ReleaseDate ?? info.Info?.ReleaseDate),
            TmdbId = series.Tmdb ?? info.Info?.Tmdb,
            Plot = series.Plot ?? info.Info?.Plot,
            Rating = series.Rating ?? info.Info?.Rating,
            Poster = series.Cover ?? info.Info?.Cover,
            Backdrop = series.BackdropPath.Concat(info.Info?.BackdropPath ?? []).FirstOrDefault(),
            Category = series.CategoryId is null ? null : CategoryNames(categories).GetValueOrDefault(series.CategoryId),
            Genres = (series.Genre ?? info.Info?.Genre)?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [],
        };

        var folder = Unique(usedFolders ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase), LibraryNaming.SeriesFolder(entry) + _folderSuffix, series.SeriesId);
        var files = new Dictionary<string, string> { [$"{folder}/tvshow.nfo"] = NfoWriter.TvShow(entry) };
        foreach (var episode in Episodes(info))
        {
            var path = $"{folder}/{LibraryNaming.SeasonFolder(episode)}/{LibraryNaming.EpisodeFile(entry, episode)}";
            if (files.TryAdd(path + ".strm", _urls.For(profile.Id, StreamKind.Episode, episode.Id, episode.Extension)))
            {
                files[path + ".nfo"] = NfoWriter.Episode(entry, episode);
            }
        }

        return new SyncItem("series:" + series.SeriesId.ToString(CultureInfo.InvariantCulture), files);
    }

    /// <summary>
    /// Drops the <c>Series - S01E01 - </c> prefix many providers put in episode titles.
    /// </summary>
    /// <param name="raw">Provider episode title.</param>
    /// <returns>The episode's own title, or null if there is none.</returns>
    public static string? EpisodeTitle(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var title = EpisodePrefix().Replace(raw, string.Empty).Trim();
        return title.Length > 0 ? title : null;
    }

    [GeneratedRegex(@"^.*?\bS\d{1,2}\s*E\d{1,3}\b\s*-?\s*", RegexOptions.IgnoreCase, 100)]
    private static partial Regex EpisodePrefix();

    [GeneratedRegex(@"\b(adult|xxx|porn|18\+)", RegexOptions.IgnoreCase, 100)]
    private static partial Regex AdultCategory();

    private static IEnumerable<EpisodeEntry> Episodes(SeriesInfo info)
        => info.Episodes
            .OrderBy(s => s.Key)
            .SelectMany(season => season.Value.Select((e, index) => new EpisodeEntry
            {
                Id = e.Id,
                Season = e.Season ?? season.Key,
                Episode = e.EpisodeNum ?? index + 1,
                Title = EpisodeTitle(e.Title),
                Extension = e.ContainerExtension ?? DefaultExtension,
                Plot = e.Info?.Plot,
                AirDate = e.Info?.ReleaseDate,
                Thumb = e.Info?.MovieImage,
                TmdbId = e.Info?.TmdbId,
                Rating = e.Info?.Rating,
            }));

    private static Dictionary<string, string> CategoryNames(IEnumerable<Category> categories)
        => categories
            .Where(c => c.CategoryId is not null && c.CategoryName is not null)
            .DistinctBy(c => c.CategoryId)
            .ToDictionary(c => c.CategoryId!, c => c.CategoryName!);

    private static bool Include(ProviderProfile profile, IEnumerable<CategoryRule> rules, Dictionary<string, string> names, string? categoryId)
    {
        if (categoryId is null)
        {
            return true;
        }

        if (rules.Any(r => r.CategoryId == categoryId && r.Excluded))
        {
            return false;
        }

        return !(profile.SkipAdultCategories && names.TryGetValue(categoryId, out var name) && AdultCategory().IsMatch(name));
    }

    private static int? ParseYear(string? date)
        => date is { Length: >= 4 } && int.TryParse(date.AsSpan(0, 4), CultureInfo.InvariantCulture, out var year) ? year : null;

    private static string Unique(ISet<string> used, string folder, int id)
    {
        var name = folder;
        if (!used.Add(name))
        {
            name = string.Create(CultureInfo.InvariantCulture, $"{folder} [{id}]");
            used.Add(name);
        }

        return name;
    }
}
