using System;
using System.Collections.ObjectModel;
using Jellyfin.Plugin.XtreamPro.Api;

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

    /// <summary>Gets or sets a value indicating whether live channels from this provider are shown.</summary>
    public bool EnableLiveTv { get; set; } = true;

    /// <summary>Gets or sets the live stream container.</summary>
    public LiveFormat LiveFormat { get; set; } = LiveFormat.Ts;

    /// <summary>Gets or sets a value indicating whether clients may play live streams directly from the provider.</summary>
    public bool PreferDirectPlay { get; set; } = true;

    /// <summary>Gets or sets a correction applied to guide times, in minutes.</summary>
    public int EpgOffsetMinutes { get; set; }

    /// <summary>Gets or sets the live category rules.</summary>
    public Collection<CategoryRule> LiveCategories { get; set; } = [];

    /// <summary>Gets or sets the channel number overrides.</summary>
    public Collection<ChannelNumberOverride> ChannelNumbers { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether movies are synced to the movies folder.</summary>
    public bool EnableVod { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether series are synced to the shows folder.</summary>
    public bool EnableSeries { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether categories that look adult are skipped.</summary>
    public bool SkipAdultCategories { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether movies are added to a collection per category.</summary>
    public bool CategoryCollections { get; set; }

    /// <summary>Gets or sets the VOD category rules.</summary>
    public Collection<CategoryRule> VodCategories { get; set; } = [];

    /// <summary>Gets or sets the series category rules.</summary>
    public Collection<CategoryRule> SeriesCategories { get; set; } = [];
}
