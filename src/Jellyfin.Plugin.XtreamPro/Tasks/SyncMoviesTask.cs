using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Sync;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.XtreamPro.Tasks;

/// <summary>
/// Syncs provider movies into the movies folder.
/// </summary>
public class SyncMoviesTask : IScheduledTask
{
    private readonly LibrarySync _sync;
    private readonly ILibraryManager _library;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncMoviesTask"/> class.
    /// </summary>
    /// <param name="sync">Library sync.</param>
    /// <param name="library">Library manager.</param>
    public SyncMoviesTask(LibrarySync sync, ILibraryManager library)
    {
        _sync = sync;
        _library = library;
    }

    /// <inheritdoc />
    public string Name => "Sync Xtream movies";

    /// <inheritdoc />
    public string Key => "XtreamProSyncMovies";

    /// <inheritdoc />
    public string Description => "Writes provider movies as .strm and .nfo files and removes ones the provider dropped.";

    /// <inheritdoc />
    public string Category => "Xtream Pro";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var report = await _sync.SyncMoviesAsync(progress, cancellationToken).ConfigureAwait(false);
        if (report.Added + report.Updated + report.Removed > 0)
        {
            _library.QueueLibraryScan();
        }
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        => [new TaskTriggerInfo { Type = TaskTriggerInfoType.IntervalTrigger, IntervalTicks = TimeSpan.FromHours(24).Ticks }];
}
