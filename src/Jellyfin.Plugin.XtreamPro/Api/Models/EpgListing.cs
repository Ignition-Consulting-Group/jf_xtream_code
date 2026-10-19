using System;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.XtreamPro.Api.Json;

namespace Jellyfin.Plugin.XtreamPro.Api.Models;

/// <summary>
/// A programme from <c>get_short_epg</c>.
/// </summary>
public sealed record EpgListing
{
    /// <summary>Gets the programme title.</summary>
    [JsonConverter(typeof(Base64StringConverter))]
    public string? Title { get; init; }

    /// <summary>Gets the programme description.</summary>
    [JsonConverter(typeof(Base64StringConverter))]
    public string? Description { get; init; }

    /// <summary>Gets the XMLTV channel id.</summary>
    public string? ChannelId { get; init; }

    /// <summary>Gets the start time.</summary>
    [JsonPropertyName("start_timestamp")]
    public DateTimeOffset? Start { get; init; }

    /// <summary>Gets the end time.</summary>
    [JsonPropertyName("stop_timestamp")]
    public DateTimeOffset? Stop { get; init; }

    /// <summary>Gets a value indicating whether catch-up exists for this programme.</summary>
    public bool HasArchive { get; init; }
}
