using System;

namespace Jellyfin.Plugin.XtreamPro.Api.Models;

/// <summary>
/// A live channel from <c>get_live_streams</c>.
/// </summary>
public sealed record LiveChannel
{
    /// <summary>Gets the provider channel number.</summary>
    public int? Num { get; init; }

    /// <summary>Gets the channel name.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the stream id.</summary>
    public int StreamId { get; init; }

    /// <summary>Gets the logo URL.</summary>
    public string? StreamIcon { get; init; }

    /// <summary>Gets the XMLTV channel id.</summary>
    public string? EpgChannelId { get; init; }

    /// <summary>Gets when the channel was added.</summary>
    public DateTimeOffset? Added { get; init; }

    /// <summary>Gets the category id.</summary>
    public string? CategoryId { get; init; }

    /// <summary>Gets a value indicating whether catch-up is available.</summary>
    public bool TvArchive { get; init; }

    /// <summary>Gets the catch-up window in days.</summary>
    public int? TvArchiveDuration { get; init; }
}
