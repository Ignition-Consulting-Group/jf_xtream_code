using System.Globalization;
using System.Linq;
using System.Text;

namespace Jellyfin.Plugin.XtreamPro.Sync;

/// <summary>
/// Folder and file names that Jellyfin's movie and show resolvers understand.
/// </summary>
public static class LibraryNaming
{
    private const int MaxLength = 120;
    private static readonly char[] Invalid = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    /// <summary>Gets the folder name for a movie.</summary>
    /// <param name="movie">The movie.</param>
    /// <returns>The folder name.</returns>
    public static string MovieFolder(MovieEntry movie) => WithTmdb(MovieFile(movie), movie.TmdbId);

    /// <summary>Gets the file name, without extension, for a movie.</summary>
    /// <param name="movie">The movie.</param>
    /// <returns>The file name.</returns>
    public static string MovieFile(MovieEntry movie) => WithYear(movie.Title, movie.Year);

    /// <summary>Gets the folder name for a series.</summary>
    /// <param name="series">The series.</param>
    /// <returns>The folder name.</returns>
    public static string SeriesFolder(SeriesEntry series) => WithTmdb(WithYear(series.Title, series.Year), series.TmdbId);

    /// <summary>Gets the season folder name for an episode.</summary>
    /// <param name="episode">The episode.</param>
    /// <returns>The folder name.</returns>
    public static string SeasonFolder(EpisodeEntry episode)
        => string.Create(CultureInfo.InvariantCulture, $"Season {episode.Season:00}");

    /// <summary>Gets the file name, without extension, for an episode.</summary>
    /// <param name="series">The series.</param>
    /// <param name="episode">The episode.</param>
    /// <returns>The file name.</returns>
    public static string EpisodeFile(SeriesEntry series, EpisodeEntry episode)
    {
        var name = string.Create(CultureInfo.InvariantCulture, $"{series.Title} - S{episode.Season:00}E{episode.Episode:00}");
        return Sanitize(string.IsNullOrWhiteSpace(episode.Title) ? name : $"{name} - {episode.Title}");
    }

    /// <summary>
    /// Removes characters that are not allowed in file names on common file systems.
    /// </summary>
    /// <param name="name">Raw name.</param>
    /// <returns>A safe name.</returns>
    public static string Sanitize(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsControl(c) || char.IsWhiteSpace(c))
            {
                builder.Append(' ');
            }
            else if (!Invalid.Contains(c))
            {
                builder.Append(c);
            }
        }

        var clean = string.Join(' ', builder.ToString().Split(' ', System.StringSplitOptions.RemoveEmptyEntries));
        if (clean.Length > MaxLength)
        {
            clean = clean[..MaxLength];
        }

        return clean.TrimEnd('.', ' ');
    }

    private static string WithYear(string title, int? year)
        => Sanitize(year is null ? title : string.Create(CultureInfo.InvariantCulture, $"{title} ({year})"));

    private static string WithTmdb(string name, string? tmdbId)
        => tmdbId is null ? name : $"{name} [tmdbid-{Sanitize(tmdbId)}]";
}
