namespace Jellyfin.Plugin.XtreamPro.Configuration;

/// <summary>
/// User-chosen guide channel for one live stream.
/// </summary>
public class EpgOverride
{
    /// <summary>Gets or sets the provider stream id.</summary>
    public int StreamId { get; set; }

    /// <summary>Gets or sets the XMLTV channel id to use.</summary>
    public string EpgChannelId { get; set; } = string.Empty;
}
