namespace Jellyfin.Plugin.XtreamPro.Web;

/// <summary>
/// Whether the web client is linked to the enhanced Live TV page, for the dashboard.
/// </summary>
public class EnhancedUiStatus
{
    /// <summary>Gets or sets a value indicating whether the loader is injected into the web client.</summary>
    public bool Injected { get; set; }

    /// <summary>Gets or sets a message explaining the current state.</summary>
    public string? Message { get; set; }
}
