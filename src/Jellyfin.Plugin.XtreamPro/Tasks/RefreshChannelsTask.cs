using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.LiveTv;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.XtreamPro.Tasks;

/// <summary>
/// Reloads live channels from each provider.
/// </summary>
public class RefreshChannelsTask : IScheduledTask
{
    private readonly LiveCatalog _catalog;
    private readonly ITaskManager _tasks;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshChannelsTask"/> class.
    /// </summary>
    /// <param name="catalog">Live channel catalog.</param>
    /// <param name="tasks">Task manager.</param>
    public RefreshChannelsTask(LiveCatalog catalog, ITaskManager tasks)
    {
        _catalog = catalog;
        _tasks = tasks;
    }

    /// <inheritdoc />
    public string Name => "Refresh Xtream channels";

    /// <inheritdoc />
    public string Key => "XtreamProRefreshChannels";

    /// <inheritdoc />
    public string Description => "Reloads live channels and categories from each Xtream provider.";

    /// <inheritdoc />
    public string Category => "Xtream Pro";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        await _catalog.GetChannelsAsync(false, cancellationToken).ConfigureAwait(false);
        progress.Report(100);
        JellyfinTasks.QueueGuideRefresh(_tasks);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        => [new TaskTriggerInfo { Type = TaskTriggerInfoType.IntervalTrigger, IntervalTicks = TimeSpan.FromHours(6).Ticks }];
}
