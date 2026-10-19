using System.Collections.Generic;

namespace Jellyfin.Plugin.XtreamPro.Sync;

/// <summary>
/// A movie to write to the library.
/// </summary>
public sealed record MovieEntry
{
    /// <summary>Gets the provider stream id.</summary>
    public required int StreamId { get; init; }

    /// <summary>Gets the clean title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the release year.</summary>
    public int? Year { get; init; }

    /// <summary>Gets the container extension.</summary>
    public required string Extension { get; init; }

    /// <summary>Gets the TMDB id.</summary>
    public string? TmdbId { get; init; }

    /// <summary>Gets the rating out of 10.</summary>
    public double? Rating { get; init; }

    /// <summary>Gets the poster URL.</summary>
    public string? Poster { get; init; }

    /// <summary>Gets the provider category name, used for collections.</summary>
    public string? Category { get; init; }

    /// <summary>Gets the genres.</summary>
    public IReadOnlyList<string> Genres { get; init; } = [];
}
