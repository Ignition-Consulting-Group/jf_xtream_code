using System;

namespace Jellyfin.Plugin.XtreamPro.Api;

/// <summary>
/// The address and login for one Xtream provider.
/// </summary>
/// <param name="BaseUrl">Provider base URL, for example <c>http://host:8080</c>.</param>
/// <param name="Username">Account username.</param>
/// <param name="Password">Account password.</param>
public sealed record XtreamCredentials(Uri BaseUrl, string Username, string Password);
