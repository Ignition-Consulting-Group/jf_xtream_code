using System.Collections.Generic;

namespace Jellyfin.Plugin.XtreamPro.Web;

/// <summary>
/// Titles to clean with a set of rules, for the live preview.
/// </summary>
public sealed class TitlePreviewRequest
{
    /// <summary>Gets or sets the cleanup patterns.</summary>
    public IReadOnlyList<string> Rules { get; set; } = [];

    /// <summary>Gets or sets the sample titles.</summary>
    public IReadOnlyList<string> Titles { get; set; } = [];
}
