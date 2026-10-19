using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.XtreamPro.Api.Json;

namespace Jellyfin.Plugin.XtreamPro.Api.Models;

/// <summary>
/// Account details from account info.
/// </summary>
public sealed record UserInfo
{
    /// <summary>Gets the username.</summary>
    public string? Username { get; init; }

    /// <summary>Gets a value indicating whether the login was accepted.</summary>
    public bool Auth { get; init; }

    /// <summary>Gets the account status, for example <c>Active</c> or <c>Expired</c>.</summary>
    public string? Status { get; init; }

    /// <summary>Gets the expiry. Null means no expiry.</summary>
    [JsonPropertyName("exp_date")]
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Gets a value indicating whether this is a trial.</summary>
    public bool IsTrial { get; init; }

    /// <summary>Gets the number of streams currently open on the account.</summary>
    [JsonPropertyName("active_cons")]
    public int? ActiveConnections { get; init; }

    /// <summary>Gets the connection limit. Null means unknown or unlimited.</summary>
    public int? MaxConnections { get; init; }

    /// <summary>Gets the output formats the account may use.</summary>
    [JsonConverter(typeof(StringOrArrayConverter))]
    public IReadOnlyList<string> AllowedOutputFormats { get; init; } = [];
}
