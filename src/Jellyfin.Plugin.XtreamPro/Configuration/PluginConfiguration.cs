using System.Collections.ObjectModel;
using Jellyfin.Plugin.XtreamPro.Sync;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.XtreamPro.Configuration;

/// <summary>
/// Plugin configuration persisted by Jellyfin.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets the configured provider accounts.</summary>
    public Collection<ProviderProfile> Providers { get; set; } = [];

    /// <summary>Gets or sets the folder movies are written to. Empty disables movie sync.</summary>
    public string MoviesPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the folder series are written to. Empty disables series sync.</summary>
    public string ShowsPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the folder catch-up programmes are written to. Empty disables catch-up.</summary>
    public string CatchUpPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the Jellyfin address written into <c>.strm</c> files. Empty uses the server's local address.</summary>
    public string StreamBaseUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the secret that signs stream URLs. Generated on first sync.</summary>
    public string StreamSecret { get; set; } = string.Empty;

    /// <summary>Gets or sets the title cleanup patterns, applied in order.</summary>
    public Collection<string> TitleRules { get; set; } = [.. TitleCleaner.DefaultRules];

    /// <summary>Gets or sets a value indicating whether the enhanced Live TV page is served and linked from the web client.</summary>
    public bool EnableEnhancedLiveTv { get; set; }

    /// <summary>Gets or sets how many days series episode lists are cached before being fetched again.</summary>
    public int SeriesRefreshDays { get; set; } = 7;
}
