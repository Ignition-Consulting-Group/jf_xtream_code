using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Api;
using Jellyfin.Plugin.XtreamPro.Api.Models;
using Jellyfin.Plugin.XtreamPro.Configuration;
using MediaBrowser.Controller.LiveTv;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.XtreamPro.LiveTv;

/// <summary>
/// Fetches and caches live channels for every enabled provider.
/// </summary>
public class LiveCatalog
{
    private readonly IXtreamSettings _settings;
    private readonly XtreamClientFactory _clients;
    private readonly GuideStore _guides;
    private readonly ILogger<LiveCatalog> _logger;
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<LiveChannel>> _streams = new();
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<ChannelInfo>> _channels = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveCatalog"/> class.
    /// </summary>
    /// <param name="settings">Plugin settings.</param>
    /// <param name="clients">Client factory.</param>
    /// <param name="guides">Guide store, for logo fallbacks.</param>
    /// <param name="logger">Logger.</param>
    public LiveCatalog(IXtreamSettings settings, XtreamClientFactory clients, GuideStore guides, ILogger<LiveCatalog> logger)
    {
        _settings = settings;
        _clients = clients;
        _guides = guides;
        _logger = logger;
    }

    /// <summary>
    /// Gets the providers with live TV enabled.
    /// </summary>
    public IEnumerable<ProviderProfile> Providers => _settings.Current.Providers.Where(p => p.EnableLiveTv);

    /// <summary>
    /// Creates a client for a provider.
    /// </summary>
    /// <param name="profile">Provider profile.</param>
    /// <returns>The client.</returns>
    public XtreamClient CreateClient(ProviderProfile profile) => _clients.Create(profile);

    /// <summary>
    /// Gets Jellyfin channels for all providers.
    /// </summary>
    /// <param name="enableCache">Whether cached channels may be returned.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The channels.</returns>
    public async Task<IReadOnlyList<ChannelInfo>> GetChannelsAsync(bool enableCache, CancellationToken cancellationToken)
    {
        var all = new List<ChannelInfo>();
        foreach (var profile in Providers)
        {
            if (!enableCache || !_channels.TryGetValue(profile.Id, out var channels))
            {
                channels = await RefreshAsync(profile, cancellationToken).ConfigureAwait(false);
            }

            all.AddRange(channels);
        }

        return all;
    }

    /// <summary>
    /// Gets the provider live streams, fetching them if not cached.
    /// </summary>
    /// <param name="profile">Provider profile.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The live streams, or empty if the provider could not be reached.</returns>
    public async Task<IReadOnlyList<LiveChannel>> GetStreamsAsync(ProviderProfile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!_streams.TryGetValue(profile.Id, out var streams))
        {
            await RefreshAsync(profile, cancellationToken).ConfigureAwait(false);
            streams = _streams.GetValueOrDefault(profile.Id, []);
        }

        return streams;
    }

    /// <summary>
    /// Resolves a Jellyfin channel id.
    /// </summary>
    /// <param name="channelId">Jellyfin channel id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The provider and stream, or null if the id is not one of ours.</returns>
    public async Task<(ProviderProfile Profile, LiveChannel Channel)?> FindAsync(string channelId, CancellationToken cancellationToken)
    {
        if (!ChannelId.TryParse(channelId, out var id)
            || Providers.FirstOrDefault(p => p.Id == id.ProviderId) is not { } profile)
        {
            return null;
        }

        var streams = await GetStreamsAsync(profile, cancellationToken).ConfigureAwait(false);
        return streams.FirstOrDefault(s => s.StreamId == id.StreamId) is { } channel ? (profile, channel) : null;
    }

    private async Task<IReadOnlyList<ChannelInfo>> RefreshAsync(ProviderProfile profile, CancellationToken cancellationToken)
    {
        try
        {
            var client = _clients.Create(profile);
            var categories = await client.GetLiveCategoriesAsync(cancellationToken).ConfigureAwait(false);
            var guideIds = profile.EpgOverrides.ToDictionary(o => o.StreamId, o => o.EpgChannelId);
            var streams = (await client.GetLiveChannelsAsync(null, cancellationToken).ConfigureAwait(false))
                .Select(s => guideIds.TryGetValue(s.StreamId, out var guideId) ? s with { EpgChannelId = guideId } : s)
                .ToList();
            var guide = await _guides.LoadAsync(profile.Id, cancellationToken).ConfigureAwait(false);
            var channels = ChannelMapper.Map(profile, categories, streams, guide?.Icons);

            _streams[profile.Id] = streams;
            _channels[profile.Id] = channels;
            return channels;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or ArgumentException or Polly.CircuitBreaker.BrokenCircuitException)
        {
            _logger.LogError(ex, "Could not load live channels from {Provider}", profile.Name);
            return _channels.GetValueOrDefault(profile.Id, []);
        }
    }
}
