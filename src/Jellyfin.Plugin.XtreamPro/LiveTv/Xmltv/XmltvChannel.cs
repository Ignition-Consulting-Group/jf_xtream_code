namespace Jellyfin.Plugin.XtreamPro.LiveTv.Xmltv;

/// <summary>
/// A <c>channel</c> element from XMLTV.
/// </summary>
/// <param name="Id">Guide channel id.</param>
/// <param name="DisplayName">First display name.</param>
/// <param name="Icon">Icon URL.</param>
public sealed record XmltvChannel(string Id, string? DisplayName, string? Icon);
