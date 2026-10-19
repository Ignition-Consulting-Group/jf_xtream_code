using System.Text.Json.Serialization;
using Jellyfin.Plugin.XtreamPro.Api.Json;

namespace Jellyfin.Plugin.XtreamPro.Api.Models;

/// <summary>
/// An episode from <c>get_series_info</c>.
/// </summary>
public sealed record Episode
{
    /// <summary>Gets the episode stream id.</summary>
    public int Id { get; init; }

    /// <summary>Gets the episode number within the season.</summary>
    public int? EpisodeNum { get; init; }

    /// <summary>Gets the title as the provider sends it.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the container extension.</summary>
    public string? ContainerExtension { get; init; }

    /// <summary>Gets the season number.</summary>
    public int? Season { get; init; }

    /// <summary>Gets the episode details. Null when the provider has none.</summary>
    [JsonConverter(typeof(EmptyArrayAsNullConverter<EpisodeDetails>))]
    public EpisodeDetails? Info { get; init; }
}
