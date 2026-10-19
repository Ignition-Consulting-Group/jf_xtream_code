using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Api;
using Jellyfin.Plugin.XtreamPro.Api.Models;
using Jellyfin.Plugin.XtreamPro.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.XtreamPro.Sync;

/// <summary>
/// Syncs provider movies and series into library folders.
/// </summary>
public class LibrarySync
{
    private const int SeriesInfoParallelism = 4;

    private readonly IXtreamSettings _settings;
    private readonly XtreamClientFactory _clients;
    private readonly string _dataDirectory;
    private readonly Func<string> _localServerUrl;
    private readonly TimeProvider _time;
    private readonly ILogger<LibrarySync> _logger;
    private readonly LibraryWriter _writer = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="LibrarySync"/> class.
    /// </summary>
    /// <param name="settings">Plugin settings.</param>
    /// <param name="clients">Client factory.</param>
    /// <param name="dataDirectory">Directory for sync state.</param>
    /// <param name="localServerUrl">Gets the server's local address, used when no stream base URL is set.</param>
    /// <param name="time">Clock.</param>
    /// <param name="logger">Logger.</param>
    public LibrarySync(IXtreamSettings settings, XtreamClientFactory clients, string dataDirectory, Func<string> localServerUrl, TimeProvider time, ILogger<LibrarySync> logger)
    {
        _settings = settings;
        _clients = clients;
        _dataDirectory = dataDirectory;
        _localServerUrl = localServerUrl;
        _time = time;
        _logger = logger;
    }

    /// <summary>
    /// Syncs movies for every provider with VOD enabled.
    /// </summary>
    /// <param name="progress">Progress from 0 to 100.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Totals across providers.</returns>
    public Task<SyncReport> SyncMoviesAsync(IProgress<double> progress, CancellationToken cancellationToken)
        => SyncAsync(_settings.Current.MoviesPath, p => p.EnableVod, "movies", PlanMoviesAsync, progress, cancellationToken);

    /// <summary>
    /// Syncs series for every provider with series enabled.
    /// </summary>
    /// <param name="progress">Progress from 0 to 100.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Totals across providers.</returns>
    public Task<SyncReport> SyncSeriesAsync(IProgress<double> progress, CancellationToken cancellationToken)
        => SyncAsync(_settings.Current.ShowsPath, p => p.EnableSeries, "series", PlanSeriesAsync, progress, cancellationToken);

    private async Task<SyncReport> SyncAsync(
        string libraryPath,
        Func<ProviderProfile, bool> enabled,
        string kind,
        Func<ProviderProfile, XtreamClient, Planner, CancellationToken, Task<(IReadOnlyList<SyncItem> Items, IReadOnlySet<string> Keep)>> plan,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        var total = new SyncReport(0, 0, 0, 0);
        var providers = _settings.Current.Providers.Where(enabled).ToList();
        if (string.IsNullOrWhiteSpace(libraryPath) || providers.Count == 0)
        {
            return total;
        }

        for (var i = 0; i < providers.Count; i++)
        {
            var profile = providers[i];
            var planner = CreatePlanner(providers.Count > 1 ? ProviderName(profile) : null);
            var share = 100.0 / providers.Count;
            var offset = i * share;
            try
            {
                var (items, keep) = await plan(profile, _clients.Create(profile), planner, cancellationToken).ConfigureAwait(false);
                var report = await _writer.ApplyAsync(
                    libraryPath,
                    Path.Combine(_dataDirectory, $"sync-{kind}-{profile.Id:N}.json"),
                    items,
                    new Progress<double>(p => progress.Report(offset + (p * share / 100))),
                    cancellationToken,
                    keep).ConfigureAwait(false);

                _logger.LogInformation("Synced {Kind} for {Provider}: {Report}", kind, profile.Name, report);
                total = new SyncReport(total.Added + report.Added, total.Updated + report.Updated, total.Removed + report.Removed, total.Unchanged + report.Unchanged);
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or ArgumentException or Polly.CircuitBreaker.BrokenCircuitException)
            {
                _logger.LogError(ex, "Could not sync {Kind} for {Provider}", kind, profile.Name);
            }
        }

        return total;
    }

    private static string ProviderName(ProviderProfile profile)
        => string.IsNullOrWhiteSpace(profile.Name) ? profile.Id.ToString("N") : profile.Name;

    private Planner CreatePlanner(string? folderTag)
    {
        var config = _settings.Current;
        if (string.IsNullOrEmpty(config.StreamSecret))
        {
            config.StreamSecret = StreamUrls.NewSecret();
            _settings.Save();
        }

        var baseUrl = string.IsNullOrWhiteSpace(config.StreamBaseUrl) ? _localServerUrl() : config.StreamBaseUrl;
        return new Planner(new TitleCleaner(config.TitleRules), new StreamUrls(baseUrl, config.StreamSecret), folderTag);
    }

    private static async Task<(IReadOnlyList<SyncItem> Items, IReadOnlySet<string> Keep)> PlanMoviesAsync(
        ProviderProfile profile,
        XtreamClient client,
        Planner planner,
        CancellationToken cancellationToken)
    {
        var categories = await client.GetVodCategoriesAsync(cancellationToken).ConfigureAwait(false);
        var movies = await client.GetVodItemsAsync(null, cancellationToken).ConfigureAwait(false);
        return (planner.Movies(profile, categories, movies), new HashSet<string>());
    }

    private async Task<(IReadOnlyList<SyncItem> Items, IReadOnlySet<string> Keep)> PlanSeriesAsync(
        ProviderProfile profile,
        XtreamClient client,
        Planner planner,
        CancellationToken cancellationToken)
    {
        var categories = await client.GetSeriesCategoriesAsync(cancellationToken).ConfigureAwait(false);
        var series = (await client.GetSeriesAsync(null, cancellationToken).ConfigureAwait(false))
            .Where(s => planner.IncludeSeries(profile, categories, s))
            .ToList();

        var cache = await SeriesInfoCache.LoadAsync(Path.Combine(_dataDirectory, $"series-info-{profile.Id:N}.json"), cancellationToken).ConfigureAwait(false);
        var now = _time.GetUtcNow();
        var maxAge = TimeSpan.FromDays(Math.Max(0, _settings.Current.SeriesRefreshDays));
        var infos = new ConcurrentDictionary<int, SeriesInfo>();
        await Parallel.ForEachAsync(
            series,
            new ParallelOptions { MaxDegreeOfParallelism = SeriesInfoParallelism, CancellationToken = cancellationToken },
            async (s, ct) =>
            {
                if (cache.GetFresh(s, now, maxAge) is { } fresh)
                {
                    infos[s.SeriesId] = fresh;
                    return;
                }

                try
                {
                    var info = await client.GetSeriesInfoAsync(s.SeriesId, ct).ConfigureAwait(false);
                    cache.Set(s, info, now);
                    infos[s.SeriesId] = info;
                }
                catch (Exception ex) when (ex is HttpRequestException or JsonException or Polly.CircuitBreaker.BrokenCircuitException)
                {
                    _logger.LogWarning(ex, "Could not fetch episodes for series {SeriesId}", s.SeriesId);
                    if (cache.GetAny(s.SeriesId) is { } stale)
                    {
                        infos[s.SeriesId] = stale;
                    }
                }
            }).ConfigureAwait(false);

        await cache.SaveAsync(series.Select(s => s.SeriesId).ToHashSet(), cancellationToken).ConfigureAwait(false);

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = series
            .Where(s => infos.ContainsKey(s.SeriesId))
            .Select(s => planner.Series(profile, categories, s, infos[s.SeriesId], used))
            .ToList();
        var keep = series.Where(s => !infos.ContainsKey(s.SeriesId)).Select(s => "series:" + s.SeriesId).ToHashSet();
        return (items, keep);
    }
}
