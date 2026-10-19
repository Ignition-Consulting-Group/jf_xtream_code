using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Sync;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.XtreamPro.Tasks;

/// <summary>
/// Syncs provider series into the shows folder.
/// </summary>
public class SyncSeriesTask : IScheduledTask
{
    private readonly LibrarySync _sync;
    private readonly ILibraryManager _library;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncSeriesTask"/> class.
    /// </summary>
    /// <param name="sync">Library sync.</param>
    /// <param name="library">Library manager.</param>
    public SyncSeriesTask(LibrarySync sync, ILibraryManager library)
    {
        _sync = sync;
        _library = library;
    }

    /// <inheritdoc />
    public string Name => "Sync Xtream series";

    /// <inheritdoc />
    public string Key => "XtreamProSyncSeries";

    /// <inheritdoc />
    public string Description => "Writes provider series and episodes as .strm and .nfo files and removes ones the provider dropped.";

    /// <inheritdoc />
    public string Category => "Xtream Pro";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var report = await _sync.SyncSeriesAsync(progress, cancellationToken).ConfigureAwait(false);
        if (report.Added + report.Updated + report.Removed > 0)
        {
            _library.QueueLibraryScan();
        }
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        => [new TaskTriggerInfo { Type = TaskTriggerInfoType.IntervalTrigger, IntervalTicks = TimeSpan.FromHours(24).Ticks }];
}
