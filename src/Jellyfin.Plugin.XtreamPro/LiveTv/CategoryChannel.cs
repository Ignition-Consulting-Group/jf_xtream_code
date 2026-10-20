using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.XtreamPro.LiveTv;

/// <summary>
/// Live channels grouped into category folders, so every client can browse category first and then channel.
/// </summary>
public class CategoryChannel : IChannel, IRequiresMediaInfoCallback
{
    private const string Other = "Other";

    private readonly LiveCatalog _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="CategoryChannel"/> class.
    /// </summary>
    /// <param name="catalog">Live channel catalog.</param>
    public CategoryChannel(LiveCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <inheritdoc />
    public string Name => "Live TV Categories";

    /// <inheritdoc />
    public string Description => "Live channels by provider category.";

    /// <inheritdoc />
    public string DataVersion => "1";

    /// <inheritdoc />
    public string HomePageUrl => string.Empty;

    /// <inheritdoc />
    public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;

    /// <inheritdoc />
    public InternalChannelFeatures GetChannelFeatures() => new()
    {
        MediaTypes = [ChannelMediaType.Video],
        ContentTypes = [ChannelMediaContentType.Clip],
    };

    /// <inheritdoc />
    public bool IsEnabledFor(string userId) => _catalog.Providers.Any();

    /// <inheritdoc />
    public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var providers = _catalog.Providers.ToDictionary(p => p.Id);
        var channels = (await _catalog.GetChannelsAsync(true, cancellationToken).ConfigureAwait(false))
            .Select(c => (Channel: c, Folder: FolderId(c)))
            .ToList();

        IReadOnlyList<ChannelItemInfo> items = string.IsNullOrEmpty(query.FolderId)
            ? channels
                .DistinctBy(c => c.Folder)
                .Select(c => Folder(c.Folder, c.Channel, providers.Count > 1 ? providers[ProviderOf(c.Channel)].Name : null))
                .ToList()
            : channels.Where(c => c.Folder == query.FolderId).Select(c => Item(c.Channel)).ToList();

        return new ChannelItemResult { Items = items, TotalRecordCount = items.Count };
    }

    /// <inheritdoc />
    public async Task<IEnumerable<MediaSourceInfo>> GetChannelItemMediaInfo(string id, CancellationToken cancellationToken)
    {
        if (await _catalog.FindAsync(id, cancellationToken).ConfigureAwait(false) is not var (profile, channel))
        {
            return [];
        }

        var source = LiveMediaSource.Create(_catalog.CreateClient(profile), profile, channel, id);
        source.RequiresOpening = false;
        source.RequiresClosing = false;
        source.SupportsDirectPlay = false;
        return [source];
    }

    /// <inheritdoc />
    public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
        => Task.FromResult(new DynamicImageResponse { HasImage = false });

    /// <inheritdoc />
    public IEnumerable<ImageType> GetSupportedChannelImages() => [];

    private static Guid ProviderOf(ChannelInfo channel) => ChannelId.TryParse(channel.Id, out var id) ? id.ProviderId : Guid.Empty;

    private static string FolderId(ChannelInfo channel)
        => string.Create(CultureInfo.InvariantCulture, $"{ProviderOf(channel):N}|{channel.ChannelGroup ?? Other}");

    private static ChannelItemInfo Folder(string id, ChannelInfo first, string? provider) => new()
    {
        Id = id,
        Name = provider is null ? first.ChannelGroup ?? Other : $"{provider} - {first.ChannelGroup ?? Other}",
        Type = ChannelItemType.Folder,
        FolderType = ChannelFolderType.Container,
    };

    private static ChannelItemInfo Item(ChannelInfo channel) => new()
    {
        Id = channel.Id,
        Name = $"{channel.Number} {channel.Name}",
        Type = ChannelItemType.Media,
        MediaType = ChannelMediaType.Video,
        ContentType = ChannelMediaContentType.Clip,
        IsLiveStream = true,
        ImageUrl = channel.ImageUrl,
        Tags = [.. channel.Tags],
    };
}
