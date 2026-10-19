using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Sync;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.XtreamPro.Tasks;

/// <summary>
/// Syncs provider movies into the movies folder.
/// </summary>
public class SyncMoviesTask : LibrarySyncTask
{
    private readonly LibrarySync _sync;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncMoviesTask"/> class.
    /// </summary>
    /// <param name="sync">Sync service.</param>
    /// <param name="library">Library manager.</param>
    /// <param name="status">Sync status store.</param>
    public SyncMoviesTask(LibrarySync sync, ILibraryManager library, SyncStatusStore status)
        : base(library, status, TimeSpan.FromHours(24))
    {
        _sync = sync;
    }

    /// <inheritdoc />
    public override string Name => "Sync Xtream movies";

    /// <inheritdoc />
    public override string Key => "XtreamProSyncMovies";

    /// <inheritdoc />
    public override string Description => "Writes provider movies as .strm and .nfo files and removes ones the provider dropped.";

    /// <inheritdoc />
    protected override Task<SyncReport> SyncAsync(IProgress<double> progress, CancellationToken cancellationToken)
        => _sync.SyncMoviesAsync(progress, cancellationToken);
}
