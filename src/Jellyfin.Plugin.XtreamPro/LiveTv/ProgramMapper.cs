using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.XtreamPro.LiveTv.Xmltv;
using MediaBrowser.Controller.LiveTv;

namespace Jellyfin.Plugin.XtreamPro.LiveTv;

/// <summary>
/// Turns guide entries into Jellyfin programmes.
/// </summary>
public static class ProgramMapper
{
    private static readonly string[] MovieWords = ["movie", "film"];
    private static readonly string[] SportsWords = ["sport", "football", "soccer", "basketball", "baseball", "cricket", "rugby", "tennis", "golf"];
    private static readonly string[] NewsWords = ["news", "journalism", "current affairs"];
    private static readonly string[] KidsWords = ["kids", "children", "family", "animation", "cartoon"];

    /// <summary>
    /// Maps a guide entry.
    /// </summary>
    /// <param name="channelId">Jellyfin channel id.</param>
    /// <param name="programme">Guide entry.</param>
    /// <returns>The programme.</returns>
    public static ProgramInfo ToProgramInfo(string channelId, XmltvProgramme programme)
    {
        ArgumentNullException.ThrowIfNull(programme);
        var categories = programme.Categories;
        return new ProgramInfo
        {
            Id = string.Create(CultureInfo.InvariantCulture, $"{channelId}_{programme.Start.ToUnixTimeSeconds()}"),
            ChannelId = channelId,
            Name = programme.Title,
            EpisodeTitle = programme.SubTitle,
            Overview = programme.Description,
            StartDate = programme.Start.UtcDateTime,
            EndDate = (programme.Stop ?? programme.Start.AddHours(1)).UtcDateTime,
            Genres = categories.ToList(),
            ImageUrl = programme.Icon,
            HasImage = programme.Icon is not null,
            IsMovie = Matches(categories, MovieWords),
            IsSports = Matches(categories, SportsWords),
            IsNews = Matches(categories, NewsWords),
            IsKids = Matches(categories, KidsWords),
            IsSeries = programme.Season is not null || programme.Episode is not null || programme.SubTitle is not null,
            SeasonNumber = programme.Season,
            EpisodeNumber = programme.Episode,
            ProductionYear = programme.Year,
        };
    }

    private static bool Matches(IEnumerable<string> categories, string[] words)
        => categories.Any(c => words.Any(w => c.Contains(w, StringComparison.OrdinalIgnoreCase)));
}
