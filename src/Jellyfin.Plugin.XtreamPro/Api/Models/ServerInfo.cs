using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.XtreamPro.Api.Models;

/// <summary>
/// Server details from account info.
/// </summary>
public sealed record ServerInfo
{
    /// <summary>Gets the server host name.</summary>
    [JsonPropertyName("url")]
    public string? Host { get; init; }

    /// <summary>Gets the HTTP port.</summary>
    public int? Port { get; init; }

    /// <summary>Gets the HTTPS port.</summary>
    public int? HttpsPort { get; init; }

    /// <summary>Gets the protocol the server prefers.</summary>
    public string? ServerProtocol { get; init; }

    /// <summary>Gets the server timezone, used for EPG and catch-up times.</summary>
    public string? Timezone { get; init; }

    /// <summary>Gets the server clock as a unix timestamp.</summary>
    public long? TimestampNow { get; init; }
}
