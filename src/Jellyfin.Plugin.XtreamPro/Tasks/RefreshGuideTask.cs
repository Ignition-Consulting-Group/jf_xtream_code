using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.LiveTv;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.XtreamPro.Tasks;

/// <summary>
/// Downloads each provider's guide and stores it for Jellyfin's guide refresh.
/// </summary>
public class RefreshGuideTask : IScheduledTask
{
    private const int ShortEpgBudget = 200;

    private readonly LiveCatalog _catalog;
    private readonly GuideStore _guides;
    private readonly ITaskManager _tasks;
    private readonly TimeProvider _time;
    private readonly ILogger<RefreshGuideTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshGuideTask"/> class.
    /// </summary>
    /// <param name="catalog">Live channel catalog.</param>
    /// <param name="guides">Guide store.</param>
    /// <param name="tasks">Task manager.</param>
    /// <param name="time">Clock.</param>
    /// <param name="logger">Logger.</param>
    public RefreshGuideTask(LiveCatalog catalog, GuideStore guides, ITaskManager tasks, TimeProvider time, ILogger<RefreshGuideTask> logger)
    {
        _catalog = catalog;
        _guides = guides;
        _tasks = tasks;
        _time = time;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Refresh Xtream guide";

    /// <inheritdoc />
    public string Key => "XtreamProRefreshGuide";

    /// <inheritdoc />
    public string Description => "Downloads the XMLTV guide from each Xtream provider and fills gaps from the short EPG.";

    /// <inheritdoc />
    public string Category => "Xtream Pro";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var providers = _catalog.Providers.ToList();
        var builder = new GuideBuilder(ShortEpgBudget, _logger);
        for (var i = 0; i < providers.Count; i++)
        {
            var profile = providers[i];
            var streams = await _catalog.GetStreamsAsync(profile, cancellationToken).ConfigureAwait(false);
            if (streams.Count > 0)
            {
                var guide = await builder.BuildAsync(profile, _catalog.CreateClient(profile), [.. streams], _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
                await _guides.SaveAsync(profile.Id, guide, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "Stored guide for {Provider}: {Channels} channels, {Missing} without guide data",
                    profile.Name,
                    guide.Programmes.Count,
                    guide.ChannelsWithoutGuide.Count);
            }

            progress.Report(100.0 * (i + 1) / providers.Count);
        }

        JellyfinTasks.QueueGuideRefresh(_tasks);
        _tasks.QueueScheduledTask<PrepareCategoriesTask>();
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        => [new TaskTriggerInfo { Type = TaskTriggerInfoType.IntervalTrigger, IntervalTicks = TimeSpan.FromHours(12).Ticks }];
}
