using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Sync;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.XtreamPro.Tasks;

/// <summary>
/// Syncs provider series into the shows folder.
/// </summary>
public class SyncSeriesTask : LibrarySyncTask
{
    private readonly LibrarySync _sync;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncSeriesTask"/> class.
    /// </summary>
    /// <param name="sync">Sync service.</param>
    /// <param name="library">Library manager.</param>
    public SyncSeriesTask(LibrarySync sync, ILibraryManager library)
        : base(library, TimeSpan.FromHours(24))
    {
        _sync = sync;
    }

    /// <inheritdoc />
    public override string Name => "Sync Xtream series";

    /// <inheritdoc />
    public override string Key => "XtreamProSyncSeries";

    /// <inheritdoc />
    public override string Description => "Writes provider series and episodes as .strm and .nfo files and removes ones the provider dropped.";

    /// <inheritdoc />
    protected override Task<SyncReport> SyncAsync(IProgress<double> progress, CancellationToken cancellationToken)
        => _sync.SyncSeriesAsync(progress, cancellationToken);
}
