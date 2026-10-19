using System.Collections.Generic;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.XtreamPro.Api.Json;

namespace Jellyfin.Plugin.XtreamPro.Api.Models;

/// <summary>
/// Response of <c>get_series_info</c>.
/// </summary>
public sealed record SeriesInfo
{
    /// <summary>Gets the series details. Null when the provider has none.</summary>
    [JsonConverter(typeof(EmptyArrayAsNullConverter<Series>))]
    public Series? Info { get; init; }

    /// <summary>Gets the episodes keyed by season number.</summary>
    [JsonConverter(typeof(SeasonEpisodesConverter))]
    public IReadOnlyDictionary<int, IReadOnlyList<Episode>> Episodes { get; init; } = new Dictionary<int, IReadOnlyList<Episode>>();
}
