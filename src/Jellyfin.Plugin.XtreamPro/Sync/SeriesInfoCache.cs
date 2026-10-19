using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Api.Models;

namespace Jellyfin.Plugin.XtreamPro.Sync;

/// <summary>
/// Remembers <c>get_series_info</c> results per provider so episodes are only fetched when needed.
/// </summary>
public class SeriesInfoCache
{
    private readonly string _path;

    private SeriesInfoCache(string path, ConcurrentDictionary<int, Entry> entries)
    {
        _path = path;
        Entries = entries;
    }

    private ConcurrentDictionary<int, Entry> Entries { get; }

    /// <summary>
    /// Loads the cache for a provider.
    /// </summary>
    /// <param name="path">Cache file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cache.</returns>
    public static async Task<SeriesInfoCache> LoadAsync(string path, CancellationToken cancellationToken)
    {
        var entries = new ConcurrentDictionary<int, Entry>();
        if (File.Exists(path))
        {
            var file = File.OpenRead(path);
            await using (file.ConfigureAwait(false))
            {
                var saved = await JsonSerializer.DeserializeAsync<Dictionary<int, Entry>>(file, cancellationToken: cancellationToken).ConfigureAwait(false);
                entries = new ConcurrentDictionary<int, Entry>(saved ?? []);
            }
        }

        return new SeriesInfoCache(path, entries);
    }

    /// <summary>
    /// Gets a cached result if it is still fresh.
    /// </summary>
    /// <param name="series">The series from <c>get_series</c>.</param>
    /// <param name="now">Current time.</param>
    /// <param name="maxAge">Refresh window.</param>
    /// <returns>The cached info, or null if it must be fetched again.</returns>
    public SeriesInfo? GetFresh(Series series, DateTimeOffset now, TimeSpan maxAge)
    {
        ArgumentNullException.ThrowIfNull(series);
        return Entries.TryGetValue(series.SeriesId, out var entry) && entry.LastModified == series.LastModified && now - entry.FetchedAt < maxAge
            ? entry.Info
            : null;
    }

    /// <summary>
    /// Gets a cached result regardless of age.
    /// </summary>
    /// <param name="seriesId">Series id.</param>
    /// <returns>The cached info, or null if never fetched.</returns>
    public SeriesInfo? GetAny(int seriesId) => Entries.TryGetValue(seriesId, out var entry) ? entry.Info : null;

    /// <summary>
    /// Stores a fetched result.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="info">The fetched info.</param>
    /// <param name="now">Current time.</param>
    public void Set(Series series, SeriesInfo info, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(series);
        Entries[series.SeriesId] = new Entry(series.LastModified, now, info);
    }

    /// <summary>
    /// Drops series that the provider no longer lists, then saves.
    /// </summary>
    /// <param name="current">Series ids still listed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task SaveAsync(IReadOnlySet<int> current, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(current);
        foreach (var id in Entries.Keys)
        {
            if (!current.Contains(id))
            {
                Entries.TryRemove(id, out _);
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var file = File.Create(_path);
        await using (file.ConfigureAwait(false))
        {
            await JsonSerializer.SerializeAsync(file, new Dictionary<int, Entry>(Entries), cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record Entry(DateTimeOffset? LastModified, DateTimeOffset FetchedAt, SeriesInfo Info);
}
