namespace Jellyfin.Plugin.XtreamPro.Web;

/// <summary>
/// A live channel with no guide data.
/// </summary>
/// <param name="StreamId">Provider stream id.</param>
/// <param name="Name">Channel name.</param>
/// <param name="EpgChannelId">Guide id the provider gave, if any.</param>
public sealed record MissingGuide(int StreamId, string? Name, string? EpgChannelId);
