using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Api;
using Jellyfin.Plugin.XtreamPro.Api.Models;
using Jellyfin.Plugin.XtreamPro.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.LiveTv;

namespace Jellyfin.Plugin.XtreamPro.LiveTv;

/// <summary>
/// Jellyfin tuner host serving Xtream live channels.
/// </summary>
public class XtreamTunerHost : ITunerHost
{
    private static readonly TimeSpan AccountMaxAge = TimeSpan.FromMinutes(10);

    private readonly LiveCatalog _catalog;
    private readonly AccountMonitor _accounts;
    private readonly ConnectionTracker _connections;

    /// <summary>
    /// Initializes a new instance of the <see cref="XtreamTunerHost"/> class.
    /// </summary>
    /// <param name="catalog">Live channel catalog.</param>
    /// <param name="accounts">Account info cache.</param>
    /// <param name="connections">Connection tracker.</param>
    public XtreamTunerHost(LiveCatalog catalog, AccountMonitor accounts, ConnectionTracker connections)
    {
        _catalog = catalog;
        _accounts = accounts;
        _connections = connections;
    }

    /// <inheritdoc />
    public string Name => "Xtream Pro";

    /// <inheritdoc />
    public string Type => ChannelId.TunerHostId;

    /// <inheritdoc />
    public bool IsSupported => true;

    /// <inheritdoc />
    public async Task<List<ChannelInfo>> GetChannels(bool enableCache, CancellationToken cancellationToken)
        => [.. await _catalog.GetChannelsAsync(enableCache, cancellationToken).ConfigureAwait(false)];

    /// <inheritdoc />
    public async Task<List<MediaSourceInfo>> GetChannelStreamMediaSources(string channelId, CancellationToken cancellationToken)
    {
        var found = await _catalog.FindAsync(channelId, cancellationToken).ConfigureAwait(false);
        return found is { } f ? [CreateMediaSource(f.Profile, f.Channel, channelId)] : [];
    }

    /// <inheritdoc />
    public async Task<ILiveStream> GetChannelStream(string channelId, string streamId, IList<ILiveStream> currentLiveStreams, CancellationToken cancellationToken)
    {
        var found = await _catalog.FindAsync(channelId, cancellationToken).ConfigureAwait(false)
            ?? throw new FileNotFoundException("Not an Xtream Pro channel.", channelId);
        var (profile, channel) = found;

        var account = await _accounts.GetAsync(profile, AccountMaxAge, cancellationToken).ConfigureAwait(false);
        if (!_connections.TryAcquire(profile.Id, account.UserInfo.MaxConnections, out var lease))
        {
            throw new LiveTvConflictException($"All {account.UserInfo.MaxConnections} connections for {profile.Name} are in use.");
        }

        return new XtreamLiveSession(CreateMediaSource(profile, channel, channelId), lease!);
    }

    /// <inheritdoc />
    public Task<List<TunerHostInfo>> DiscoverDevices(int discoveryDurationMs, CancellationToken cancellationToken)
        => Task.FromResult(new List<TunerHostInfo>());

    private MediaSourceInfo CreateMediaSource(ProviderProfile profile, LiveChannel channel, string channelId)
        => LiveMediaSource.Create(_catalog.CreateClient(profile), profile, channel, channelId);
}
