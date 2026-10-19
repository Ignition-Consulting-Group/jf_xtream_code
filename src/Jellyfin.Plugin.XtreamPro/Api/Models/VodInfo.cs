using System.Text.Json.Serialization;
using Jellyfin.Plugin.XtreamPro.Api.Json;

namespace Jellyfin.Plugin.XtreamPro.Api.Models;

/// <summary>
/// Response of <c>get_vod_info</c>.
/// </summary>
public sealed record VodInfo
{
    /// <summary>Gets the movie details. Null when the provider has none.</summary>
    [JsonConverter(typeof(EmptyArrayAsNullConverter<VodDetails>))]
    public VodDetails? Info { get; init; }

    /// <summary>Gets the stream data.</summary>
    public VodItem? MovieData { get; init; }
}
