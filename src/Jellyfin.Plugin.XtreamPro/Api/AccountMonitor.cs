using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Api.Models;
using Jellyfin.Plugin.XtreamPro.Configuration;

namespace Jellyfin.Plugin.XtreamPro.Api;

/// <summary>
/// Caches account info per provider, for connection limits and status display.
/// </summary>
public class AccountMonitor
{
    private readonly XtreamClientFactory _clients;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<Guid, (AccountInfo Info, DateTimeOffset FetchedAt)> _accounts = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountMonitor"/> class.
    /// </summary>
    /// <param name="clients">Client factory.</param>
    /// <param name="time">Clock.</param>
    public AccountMonitor(XtreamClientFactory clients, TimeProvider time)
    {
        _clients = clients;
        _time = time;
    }

    /// <summary>
    /// Gets account info, fetching it if the cached copy is older than <paramref name="maxAge"/>.
    /// </summary>
    /// <param name="profile">Provider profile.</param>
    /// <param name="maxAge">Maximum age of a cached result.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The account info.</returns>
    public async Task<AccountInfo> GetAsync(ProviderProfile profile, TimeSpan maxAge, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var now = _time.GetUtcNow();
        if (_accounts.TryGetValue(profile.Id, out var cached) && now - cached.FetchedAt < maxAge)
        {
            return cached.Info;
        }

        var info = await _clients.Create(profile).GetAccountInfoAsync(cancellationToken).ConfigureAwait(false);
        _accounts[profile.Id] = (info, now);
        return info;
    }

    /// <summary>
    /// Gets the last fetched account info without calling the provider.
    /// </summary>
    /// <param name="providerId">Provider profile id.</param>
    /// <returns>The account info, or null if never fetched.</returns>
    public AccountInfo? Last(Guid providerId) => _accounts.TryGetValue(providerId, out var cached) ? cached.Info : null;
}
