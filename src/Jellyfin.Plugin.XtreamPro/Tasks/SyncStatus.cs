using System;
using Jellyfin.Plugin.XtreamPro.Sync;

namespace Jellyfin.Plugin.XtreamPro.Tasks;

/// <summary>
/// The last result of a sync task.
/// </summary>
/// <param name="FinishedAt">When it finished.</param>
/// <param name="Report">What it changed.</param>
public sealed record SyncStatus(DateTimeOffset FinishedAt, SyncReport Report);
