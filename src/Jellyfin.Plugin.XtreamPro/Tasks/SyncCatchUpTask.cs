using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.CatchUp;
using Jellyfin.Plugin.XtreamPro.Sync;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.XtreamPro.Tasks;

/// <summary>
/// Keeps the catch-up folder in step with each archive channel's window.
/// </summary>
public class SyncCatchUpTask : LibrarySyncTask
{
    private readonly CatchUpSync _sync;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncCatchUpTask"/> class.
    /// </summary>
    /// <param name="sync">Sync service.</param>
    /// <param name="library">Library manager.</param>
    /// <param name="status">Sync status store.</param>
    public SyncCatchUpTask(CatchUpSync sync, ILibraryManager library, SyncStatusStore status)
        : base(library, status, TimeSpan.FromHours(1))
    {
        _sync = sync;
    }

    /// <inheritdoc />
    public override string Name => "Sync Xtream catch-up";

    /// <inheritdoc />
    public override string Key => "XtreamProSyncCatchUp";

    /// <inheritdoc />
    public override string Description => "Writes past programmes on catch-up channels as episodes and removes ones older than the archive window.";

    /// <inheritdoc />
    protected override Task<SyncReport> SyncAsync(IProgress<double> progress, CancellationToken cancellationToken)
        => _sync.SyncAsync(progress, cancellationToken);
}
