using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.LiveTv;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.XtreamPro.Tasks;

/// <summary>
/// Opens every Live TV Categories folder in the background, so Jellyfin has saved its channels before a user opens it.
/// </summary>
/// <remarks>
/// Jellyfin saves every item in a channel folder the first time it is opened, which takes about a minute for a few thousand channels.
/// </remarks>
public class PrepareCategoriesTask : IScheduledTask
{
    private readonly IChannelManager _channels;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrepareCategoriesTask"/> class.
    /// </summary>
    /// <param name="channels">Jellyfin channel manager.</param>
    public PrepareCategoriesTask(IChannelManager channels)
    {
        _channels = channels;
    }

    /// <inheritdoc />
    public string Name => "Prepare Xtream categories";

    /// <inheritdoc />
    public string Key => "XtreamProPrepareCategories";

    /// <inheritdoc />
    public string Description => "Loads the channels in every Live TV Categories folder ahead of time, so categories open quickly.";

    /// <inheritdoc />
    public string Category => "Xtream Pro";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var channel = (await _channels.GetChannelsInternalAsync(new ChannelQuery()).ConfigureAwait(false)).Items
            .FirstOrDefault(c => c.Name == CategoryChannel.ChannelName);
        if (channel is null)
        {
            return;
        }

        var folders = (await OpenAsync(channel.Id, Guid.Empty, cancellationToken).ConfigureAwait(false)).OfType<Folder>().ToList();
        for (var i = 0; i < folders.Count; i++)
        {
            await OpenAsync(channel.Id, folders[i].Id, cancellationToken).ConfigureAwait(false);
            progress.Report(100.0 * (i + 1) / folders.Count);
        }
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => [];

    private async Task<IReadOnlyList<BaseItem>> OpenAsync(Guid channelId, Guid folderId, CancellationToken cancellationToken)
    {
        var query = new InternalItemsQuery { ChannelIds = [channelId], ParentId = folderId, EnableTotalRecordCount = false };
        return (await _channels.GetChannelItemsInternal(query, new Progress<double>(), cancellationToken).ConfigureAwait(false)).Items;
    }
}
