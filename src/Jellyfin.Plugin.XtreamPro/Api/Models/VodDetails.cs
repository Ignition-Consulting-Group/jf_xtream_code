using System.Collections.Generic;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.XtreamPro.Api.Json;

namespace Jellyfin.Plugin.XtreamPro.Api.Models;

/// <summary>
/// Movie details from <c>get_vod_info</c>.
/// </summary>
public sealed record VodDetails
{
    /// <summary>Gets the TMDB id.</summary>
    public string? TmdbId { get; init; }

    /// <summary>Gets the title.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the original title.</summary>
    public string? OName { get; init; }

    /// <summary>Gets the plot.</summary>
    public string? Plot { get; init; }

    /// <summary>Gets the comma separated genres.</summary>
    public string? Genre { get; init; }

    /// <summary>Gets the comma separated cast.</summary>
    public string? Cast { get; init; }

    /// <summary>Gets the director.</summary>
    public string? Director { get; init; }

    /// <summary>Gets the release date as sent by the provider.</summary>
    [JsonPropertyName("releasedate")]
    public string? ReleaseDate { get; init; }

    /// <summary>Gets the rating out of 10.</summary>
    public double? Rating { get; init; }

    /// <summary>Gets the poster URL.</summary>
    public string? MovieImage { get; init; }

    /// <summary>Gets the backdrop URLs.</summary>
    [JsonConverter(typeof(StringOrArrayConverter))]
    public IReadOnlyList<string> BackdropPath { get; init; } = [];

    /// <summary>Gets the runtime in seconds.</summary>
    public int? DurationSecs { get; init; }
}
