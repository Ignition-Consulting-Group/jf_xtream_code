using System;

namespace Jellyfin.Plugin.XtreamPro.Web;

/// <summary>
/// Result of testing a provider login.
/// </summary>
/// <param name="Ok">Whether the provider accepted the login.</param>
/// <param name="Status">Account status, for example <c>Active</c>.</param>
/// <param name="ExpiresAt">Account expiry, or null for none.</param>
/// <param name="IsTrial">Whether the account is a trial.</param>
/// <param name="MaxConnections">Connection limit.</param>
/// <param name="ActiveConnections">Connections open on the account, from any device.</param>
/// <param name="ServerConnections">Connections this server has open.</param>
/// <param name="Error">Why the test failed.</param>
public sealed record ConnectionTestResult(
    bool Ok,
    string? Status = null,
    DateTimeOffset? ExpiresAt = null,
    bool IsTrial = false,
    int? MaxConnections = null,
    int? ActiveConnections = null,
    int ServerConnections = 0,
    string? Error = null);
