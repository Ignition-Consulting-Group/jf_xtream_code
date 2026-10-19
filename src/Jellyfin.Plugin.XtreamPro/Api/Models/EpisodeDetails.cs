using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.XtreamPro.Api.Models;

/// <summary>
/// Episode details from <c>get_series_info</c>.
/// </summary>
public sealed record EpisodeDetails
{
    /// <summary>Gets the TMDB id.</summary>
    public string? TmdbId { get; init; }

    /// <summary>Gets the plot.</summary>
    public string? Plot { get; init; }

    /// <summary>Gets the air date as sent by the provider.</summary>
    [JsonPropertyName("releasedate")]
    public string? ReleaseDate { get; init; }

    /// <summary>Gets the rating out of 10.</summary>
    public double? Rating { get; init; }

    /// <summary>Gets the still image URL.</summary>
    public string? MovieImage { get; init; }

    /// <summary>Gets the runtime in seconds.</summary>
    public int? DurationSecs { get; init; }
}
