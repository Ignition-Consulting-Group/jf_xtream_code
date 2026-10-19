using System.Collections.Generic;

namespace Jellyfin.Plugin.XtreamPro.Api.Models;

/// <summary>
/// Response of <c>get_short_epg</c>.
/// </summary>
public sealed record ShortEpg
{
    /// <summary>Gets the programmes.</summary>
    public IReadOnlyList<EpgListing> EpgListings { get; init; } = [];
}
