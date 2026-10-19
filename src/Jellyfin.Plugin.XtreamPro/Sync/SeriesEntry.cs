using System.Collections.Generic;

namespace Jellyfin.Plugin.XtreamPro.Sync;

/// <summary>
/// A series to write to the library.
/// </summary>
public sealed record SeriesEntry
{
    /// <summary>Gets the provider series id.</summary>
    public required int SeriesId { get; init; }

    /// <summary>Gets the clean title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the first air year.</summary>
    public int? Year { get; init; }

    /// <summary>Gets the TMDB id.</summary>
    public string? TmdbId { get; init; }

    /// <summary>Gets the plot.</summary>
    public string? Plot { get; init; }

    /// <summary>Gets the rating out of 10.</summary>
    public double? Rating { get; init; }

    /// <summary>Gets the poster URL.</summary>
    public string? Poster { get; init; }

    /// <summary>Gets the backdrop URL.</summary>
    public string? Backdrop { get; init; }

    /// <summary>Gets the provider category name.</summary>
    public string? Category { get; init; }

    /// <summary>Gets the genres.</summary>
    public IReadOnlyList<string> Genres { get; init; } = [];

    /// <summary>Gets the episodes.</summary>
    public IReadOnlyList<EpisodeEntry> Episodes { get; init; } = [];
}
