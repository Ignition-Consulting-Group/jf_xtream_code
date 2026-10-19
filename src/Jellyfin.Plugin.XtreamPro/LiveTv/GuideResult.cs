using System.Collections.Generic;
using Jellyfin.Plugin.XtreamPro.LiveTv.Xmltv;

namespace Jellyfin.Plugin.XtreamPro.LiveTv;

/// <summary>
/// The guide for one provider.
/// </summary>
/// <param name="Programmes">Programmes keyed by Jellyfin channel id, in start order.</param>
/// <param name="Icons">Channel icons from XMLTV, keyed by guide channel id.</param>
/// <param name="ChannelsWithoutGuide">Jellyfin channel ids that have no guide data.</param>
public sealed record GuideResult(
    IReadOnlyDictionary<string, IReadOnlyList<XmltvProgramme>> Programmes,
    IReadOnlyDictionary<string, string> Icons,
    IReadOnlyList<string> ChannelsWithoutGuide);
