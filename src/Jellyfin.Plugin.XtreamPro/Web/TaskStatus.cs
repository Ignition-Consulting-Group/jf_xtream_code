using System;
using Jellyfin.Plugin.XtreamPro.Sync;

namespace Jellyfin.Plugin.XtreamPro.Web;

/// <summary>
/// State of one Xtream Pro scheduled task.
/// </summary>
/// <param name="Id">Jellyfin task id, for running it.</param>
/// <param name="Key">Task key.</param>
/// <param name="Name">Task name.</param>
/// <param name="State">Idle, Running or Cancelling.</param>
/// <param name="LastRun">When it last finished.</param>
/// <param name="LastResult">Completed, Failed, Cancelled or Aborted.</param>
/// <param name="Error">Error from the last run.</param>
/// <param name="Report">What the last sync changed, for sync tasks.</param>
public sealed record TaskStatus(string Id, string Key, string Name, string State, DateTime? LastRun, string? LastResult, string? Error, SyncReport? Report);
