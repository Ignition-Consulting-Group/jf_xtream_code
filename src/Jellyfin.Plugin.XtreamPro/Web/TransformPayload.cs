namespace Jellyfin.Plugin.XtreamPro.Web;

/// <summary>
/// The object File Transformation passes to a transformation callback.
/// </summary>
public sealed class TransformPayload
{
    /// <summary>Gets or sets the file contents.</summary>
    public string? Contents { get; set; }
}
