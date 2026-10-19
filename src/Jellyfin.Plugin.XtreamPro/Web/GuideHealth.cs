using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.XtreamPro.Web;

/// <summary>
/// Guide coverage for one provider.
/// </summary>
/// <param name="ProviderId">Provider profile id.</param>
/// <param name="Provider">Provider name.</param>
/// <param name="Channels">Live channels.</param>
/// <param name="Missing">Channels without guide data.</param>
public sealed record GuideHealth(Guid ProviderId, string Provider, int Channels, IReadOnlyList<MissingGuide> Missing);
