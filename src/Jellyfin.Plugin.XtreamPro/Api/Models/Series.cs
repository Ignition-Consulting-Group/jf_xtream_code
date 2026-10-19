using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.XtreamPro.Api.Json;

namespace Jellyfin.Plugin.XtreamPro.Api.Models;

/// <summary>
/// A series from <c>get_series</c>, also used as the <c>info</c> block of <c>get_series_info</c>.
/// </summary>
public sealed record Series
{
    /// <summary>Gets the provider order number.</summary>
    public int? Num { get; init; }

    /// <summary>Gets the title as the provider sends it.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the series id.</summary>
    public int SeriesId { get; init; }

    /// <summary>Gets the poster URL.</summary>
    public string? Cover { get; init; }

    /// <summary>Gets the plot.</summary>
    public string? Plot { get; init; }

    /// <summary>Gets the comma separated cast.</summary>
    public string? Cast { get; init; }

    /// <summary>Gets the director.</summary>
    public string? Director { get; init; }

    /// <summary>Gets the comma separated genres.</summary>
    public string? Genre { get; init; }

    /// <summary>Gets the first air date as sent by the provider.</summary>
    [JsonPropertyName("releasedate")]
    public string? ReleaseDate { get; init; }

    /// <summary>Gets when the provider last changed the series.</summary>
    public DateTimeOffset? LastModified { get; init; }

    /// <summary>Gets the rating out of 10.</summary>
    public double? Rating { get; init; }

    /// <summary>Gets the TMDB id.</summary>
    public string? Tmdb { get; init; }

    /// <summary>Gets the backdrop URLs.</summary>
    [JsonConverter(typeof(StringOrArrayConverter))]
    public IReadOnlyList<string> BackdropPath { get; init; } = [];

    /// <summary>Gets the category id.</summary>
    public string? CategoryId { get; init; }
}
