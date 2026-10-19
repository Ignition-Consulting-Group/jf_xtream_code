using System;

namespace Jellyfin.Plugin.XtreamPro.Api.Models;

/// <summary>
/// A movie from <c>get_vod_streams</c>.
/// </summary>
public sealed record VodItem
{
    /// <summary>Gets the provider order number.</summary>
    public int? Num { get; init; }

    /// <summary>Gets the title as the provider sends it.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the stream id.</summary>
    public int StreamId { get; init; }

    /// <summary>Gets the poster URL.</summary>
    public string? StreamIcon { get; init; }

    /// <summary>Gets the rating out of 10.</summary>
    public double? Rating { get; init; }

    /// <summary>Gets when the movie was added.</summary>
    public DateTimeOffset? Added { get; init; }

    /// <summary>Gets the category id.</summary>
    public string? CategoryId { get; init; }

    /// <summary>Gets the container extension, for example <c>mkv</c>.</summary>
    public string? ContainerExtension { get; init; }

    /// <summary>Gets the TMDB id.</summary>
    public string? Tmdb { get; init; }
}
