using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.XtreamPro.LiveTv.Xmltv;

/// <summary>
/// A <c>programme</c> element from XMLTV.
/// </summary>
public sealed record XmltvProgramme
{
    /// <summary>Gets the guide channel id.</summary>
    public required string ChannelId { get; init; }

    /// <summary>Gets the start time.</summary>
    public required DateTimeOffset Start { get; init; }

    /// <summary>Gets the end time, if the guide has one.</summary>
    public DateTimeOffset? Stop { get; init; }

    /// <summary>Gets the title.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the episode title.</summary>
    public string? SubTitle { get; init; }

    /// <summary>Gets the description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the categories.</summary>
    public IReadOnlyList<string> Categories { get; init; } = [];

    /// <summary>Gets the image URL.</summary>
    public string? Icon { get; init; }

    /// <summary>Gets the one-based season number.</summary>
    public int? Season { get; init; }

    /// <summary>Gets the one-based episode number.</summary>
    public int? Episode { get; init; }

    /// <summary>Gets the production year.</summary>
    public int? Year { get; init; }
}
