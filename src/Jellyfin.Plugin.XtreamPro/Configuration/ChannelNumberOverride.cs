namespace Jellyfin.Plugin.XtreamPro.Configuration;

/// <summary>
/// User-chosen channel number for one live stream.
/// </summary>
public class ChannelNumberOverride
{
    /// <summary>Gets or sets the provider stream id.</summary>
    public int StreamId { get; set; }

    /// <summary>Gets or sets the channel number shown in Jellyfin.</summary>
    public string Number { get; set; } = string.Empty;
}
