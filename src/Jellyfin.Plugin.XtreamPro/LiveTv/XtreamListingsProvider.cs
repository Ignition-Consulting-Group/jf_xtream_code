using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.LiveTv;

namespace Jellyfin.Plugin.XtreamPro.LiveTv;

/// <summary>
/// Jellyfin guide provider serving the stored Xtream guide.
/// </summary>
public class XtreamListingsProvider : IListingsProvider
{
    private readonly LiveCatalog _catalog;
    private readonly GuideStore _guides;

    /// <summary>
    /// Initializes a new instance of the <see cref="XtreamListingsProvider"/> class.
    /// </summary>
    /// <param name="catalog">Live channel catalog.</param>
    /// <param name="guides">Guide store.</param>
    public XtreamListingsProvider(LiveCatalog catalog, GuideStore guides)
    {
        _catalog = catalog;
        _guides = guides;
    }

    /// <inheritdoc />
    public string Name => "Xtream Pro";

    /// <inheritdoc />
    public string Type => ChannelId.TunerHostId;

    /// <inheritdoc />
    public async Task<IEnumerable<ProgramInfo>> GetProgramsAsync(ListingsProviderInfo info, string channelId, DateTime startDateUtc, DateTime endDateUtc, CancellationToken cancellationToken)
    {
        if (!ChannelId.TryParse(channelId, out var id))
        {
            return [];
        }

        var programmes = await _guides.GetAsync(id.ProviderId, channelId, startDateUtc, endDateUtc, cancellationToken).ConfigureAwait(false);
        return programmes.Select(p => ProgramMapper.ToProgramInfo(channelId, p));
    }

    /// <inheritdoc />
    public Task Validate(ListingsProviderInfo info, bool validateLogin, bool validateListings) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<List<NameIdPair>> GetLineups(ListingsProviderInfo info, string country, string location) => Task.FromResult(new List<NameIdPair>());

    /// <inheritdoc />
    public async Task<List<ChannelInfo>> GetChannels(ListingsProviderInfo info, CancellationToken cancellationToken)
    {
        var channels = await _catalog.GetChannelsAsync(true, cancellationToken).ConfigureAwait(false);
        return channels.Select(c => new ChannelInfo { Id = c.Id, Name = c.Name, Number = c.Number, ImageUrl = c.ImageUrl }).ToList();
    }
}
