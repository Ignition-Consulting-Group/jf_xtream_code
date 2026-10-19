namespace Jellyfin.Plugin.XtreamPro.Sync;

/// <summary>
/// An episode to write to the library.
/// </summary>
public sealed record EpisodeEntry
{
    /// <summary>Gets the provider episode id.</summary>
    public required int Id { get; init; }

    /// <summary>Gets the season number.</summary>
    public required int Season { get; init; }

    /// <summary>Gets the episode number.</summary>
    public required int Episode { get; init; }

    /// <summary>Gets the episode title.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the container extension.</summary>
    public required string Extension { get; init; }

    /// <summary>Gets the plot.</summary>
    public string? Plot { get; init; }

    /// <summary>Gets the air date.</summary>
    public string? AirDate { get; init; }

    /// <summary>Gets the still image URL.</summary>
    public string? Thumb { get; init; }

    /// <summary>Gets the TMDB id.</summary>
    public string? TmdbId { get; init; }

    /// <summary>Gets the rating out of 10.</summary>
    public double? Rating { get; init; }
}
