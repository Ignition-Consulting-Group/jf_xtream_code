using System;
using System.Collections.Generic;
using System.Threading;

namespace Jellyfin.Plugin.XtreamPro.Api;

/// <summary>
/// Counts the streams this server has open per provider, so playback respects <c>max_connections</c>.
/// </summary>
public class ConnectionTracker
{
    private readonly Dictionary<Guid, int> _active = [];
    private readonly Lock _lock = new();

    /// <summary>
    /// Gets the number of streams currently open for a provider.
    /// </summary>
    /// <param name="providerId">Provider profile id.</param>
    /// <returns>The open stream count.</returns>
    public int Active(Guid providerId)
    {
        lock (_lock)
        {
            return _active.GetValueOrDefault(providerId);
        }
    }

    /// <summary>
    /// Takes a connection slot if one is free.
    /// </summary>
    /// <param name="providerId">Provider profile id.</param>
    /// <param name="maxConnections">Provider limit, or null for no limit.</param>
    /// <param name="lease">The slot, released on dispose.</param>
    /// <returns>True if a slot was taken.</returns>
    public bool TryAcquire(Guid providerId, int? maxConnections, out IDisposable? lease)
    {
        lock (_lock)
        {
            var current = _active.GetValueOrDefault(providerId);
            if (current >= maxConnections)
            {
                lease = null;
                return false;
            }

            _active[providerId] = current + 1;
        }

        lease = new Lease(this, providerId);
        return true;
    }

    private void Release(Guid providerId)
    {
        lock (_lock)
        {
            _active[providerId]--;
        }
    }

    private sealed class Lease(ConnectionTracker tracker, Guid providerId) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                tracker.Release(providerId);
            }
        }
    }
}
