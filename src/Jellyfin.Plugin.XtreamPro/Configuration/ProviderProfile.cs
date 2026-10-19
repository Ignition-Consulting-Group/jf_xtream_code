using System;

namespace Jellyfin.Plugin.XtreamPro.Configuration;

/// <summary>
/// One Xtream provider account.
/// </summary>
public class ProviderProfile
{
    /// <summary>
    /// User-Agent used when a profile does not set one.
    /// </summary>
    public const string DefaultUserAgent = "VLC/3.0.21 LibVLC/3.0.21";

    /// <summary>Gets or sets the profile id.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the provider base URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets the username.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Gets or sets the password.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Gets or sets the User-Agent sent to this provider.</summary>
    public string UserAgent { get; set; } = DefaultUserAgent;
}
