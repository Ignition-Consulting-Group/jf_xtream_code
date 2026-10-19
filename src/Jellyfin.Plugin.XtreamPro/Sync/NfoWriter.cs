using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace Jellyfin.Plugin.XtreamPro.Sync;

/// <summary>
/// Writes Kodi-style <c>.nfo</c> files that Jellyfin's NFO reader understands.
/// </summary>
public static class NfoWriter
{
    private static readonly XmlWriterSettings Settings = new()
    {
        Indent = true,
        Encoding = new UTF8Encoding(false),
        OmitXmlDeclaration = false,
    };

    /// <summary>Writes <c>movie.nfo</c>.</summary>
    /// <param name="movie">The movie.</param>
    /// <param name="categoryCollection">Whether to add the movie to a collection named after its category.</param>
    /// <returns>The file content.</returns>
    public static string Movie(MovieEntry movie, bool categoryCollection = false) => Write("movie", w =>
    {
        Element(w, "title", movie.Title);
        Element(w, "year", movie.Year);
        Element(w, "rating", movie.Rating);
        UniqueId(w, movie.TmdbId);
        Genres(w, movie.Genres);
        Element(w, "thumb", movie.Poster);
        if (categoryCollection && movie.Category is not null)
        {
            w.WriteStartElement("set");
            Element(w, "name", movie.Category);
            w.WriteEndElement();
        }
    });

    /// <summary>Writes <c>tvshow.nfo</c>.</summary>
    /// <param name="series">The series.</param>
    /// <returns>The file content.</returns>
    public static string TvShow(SeriesEntry series) => Write("tvshow", w =>
    {
        Element(w, "title", series.Title);
        Element(w, "year", series.Year);
        Element(w, "plot", series.Plot);
        Element(w, "rating", series.Rating);
        UniqueId(w, series.TmdbId);
        Genres(w, series.Genres);
        Element(w, "thumb", series.Poster);
        if (series.Backdrop is not null)
        {
            w.WriteStartElement("fanart");
            Element(w, "thumb", series.Backdrop);
            w.WriteEndElement();
        }
    });

    /// <summary>Writes the <c>.nfo</c> that sits next to an episode.</summary>
    /// <param name="series">The series.</param>
    /// <param name="episode">The episode.</param>
    /// <returns>The file content.</returns>
    public static string Episode(SeriesEntry series, EpisodeEntry episode) => Write("episodedetails", w =>
    {
        Element(w, "title", episode.Title);
        Element(w, "showtitle", series.Title);
        Element(w, "season", episode.Season);
        Element(w, "episode", episode.Episode);
        Element(w, "plot", episode.Plot);
        Element(w, "aired", episode.AirDate);
        Element(w, "rating", episode.Rating);
        UniqueId(w, episode.TmdbId);
        Element(w, "thumb", episode.Thumb);
    });

    private static string Write(string root, System.Action<XmlWriter> body)
    {
        using var text = new Utf8StringWriter();
        using (var writer = XmlWriter.Create(text, Settings))
        {
            writer.WriteStartElement(root);
            body(writer);
            writer.WriteEndElement();
        }

        return text.ToString();
    }

    private static void Element(XmlWriter writer, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            writer.WriteElementString(name, value);
        }
    }

    private static void Element(XmlWriter writer, string name, double? value)
        => Element(writer, name, value?.ToString(CultureInfo.InvariantCulture));

    private static void UniqueId(XmlWriter writer, string? tmdbId)
    {
        if (string.IsNullOrWhiteSpace(tmdbId))
        {
            return;
        }

        writer.WriteStartElement("uniqueid");
        writer.WriteAttributeString("type", "tmdb");
        writer.WriteAttributeString("default", "true");
        writer.WriteString(tmdbId);
        writer.WriteEndElement();
    }

    private static void Genres(XmlWriter writer, IEnumerable<string> genres)
    {
        foreach (var genre in genres)
        {
            Element(writer, "genre", genre);
        }
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public Utf8StringWriter()
            : base(CultureInfo.InvariantCulture)
        {
        }

        public override Encoding Encoding => new UTF8Encoding(false);
    }
}
