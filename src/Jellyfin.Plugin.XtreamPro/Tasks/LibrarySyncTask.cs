using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Sync;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.XtreamPro.Tasks;

/// <summary>
/// A task that writes library files and asks Jellyfin to scan when anything changed.
/// </summary>
public abstract class LibrarySyncTask : IScheduledTask
{
    private readonly ILibraryManager _library;
    private readonly TimeSpan _interval;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibrarySyncTask"/> class.
    /// </summary>
    /// <param name="library">Library manager.</param>
    /// <param name="interval">Default run interval.</param>
    protected LibrarySyncTask(ILibraryManager library, TimeSpan interval)
    {
        _library = library;
        _interval = interval;
    }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Key { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public string Category => "Xtream Pro";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var report = await SyncAsync(progress, cancellationToken).ConfigureAwait(false);
        if (report.Added + report.Updated + report.Removed > 0)
        {
            _library.QueueLibraryScan();
        }
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        => [new TaskTriggerInfo { Type = TaskTriggerInfoType.IntervalTrigger, IntervalTicks = _interval.Ticks }];

    /// <summary>
    /// Runs the sync.
    /// </summary>
    /// <param name="progress">Progress from 0 to 100.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>What changed.</returns>
    protected abstract Task<SyncReport> SyncAsync(IProgress<double> progress, CancellationToken cancellationToken);
}
