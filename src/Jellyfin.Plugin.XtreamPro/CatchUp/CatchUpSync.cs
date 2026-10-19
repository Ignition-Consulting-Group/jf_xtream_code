using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Configuration;
using Jellyfin.Plugin.XtreamPro.LiveTv;
using Jellyfin.Plugin.XtreamPro.Sync;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.XtreamPro.CatchUp;

/// <summary>
/// Keeps the catch-up folder in step with each provider's archive window.
/// </summary>
public class CatchUpSync
{
    private readonly IXtreamSettings _settings;
    private readonly LiveCatalog _catalog;
    private readonly GuideStore _guides;
    private readonly string _dataDirectory;
    private readonly Func<string> _localServerUrl;
    private readonly TimeProvider _time;
    private readonly ILogger<CatchUpSync> _logger;
    private readonly LibraryWriter _writer = new(emptyIsValid: true);

    /// <summary>
    /// Initializes a new instance of the <see cref="CatchUpSync"/> class.
    /// </summary>
    /// <param name="settings">Plugin settings.</param>
    /// <param name="catalog">Live channel catalog.</param>
    /// <param name="guides">Guide store.</param>
    /// <param name="dataDirectory">Directory for sync state.</param>
    /// <param name="localServerUrl">Gets the server's local address, used when no stream base URL is set.</param>
    /// <param name="time">Clock.</param>
    /// <param name="logger">Logger.</param>
    public CatchUpSync(IXtreamSettings settings, LiveCatalog catalog, GuideStore guides, string dataDirectory, Func<string> localServerUrl, TimeProvider time, ILogger<CatchUpSync> logger)
    {
        _settings = settings;
        _catalog = catalog;
        _guides = guides;
        _dataDirectory = dataDirectory;
        _localServerUrl = localServerUrl;
        _time = time;
        _logger = logger;
    }

    /// <summary>
    /// Writes new past programmes and removes ones that left the archive window.
    /// </summary>
    /// <param name="progress">Progress from 0 to 100.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Totals across providers.</returns>
    public async Task<SyncReport> SyncAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var total = new SyncReport(0, 0, 0, 0);
        var path = _settings.Current.CatchUpPath;
        var providers = _catalog.Providers.Where(p => p.EnableCatchUp).ToList();
        if (string.IsNullOrWhiteSpace(path) || providers.Count == 0)
        {
            return total;
        }

        var planner = new CatchUpPlanner(StreamUrls.FromSettings(_settings, _localServerUrl), _time.LocalTimeZone);
        for (var i = 0; i < providers.Count; i++)
        {
            var profile = providers[i];
            var guide = await _guides.LoadAsync(profile.Id, cancellationToken).ConfigureAwait(false);
            if (guide is null)
            {
                continue;
            }

            var streams = await _catalog.GetStreamsAsync(profile, cancellationToken).ConfigureAwait(false);
            var items = planner.Plan(profile, streams, guide, _time.GetUtcNow());
            var offset = i * 100.0 / providers.Count;
            var report = await _writer.ApplyAsync(
                path,
                Path.Combine(_dataDirectory, $"sync-catchup-{profile.Id:N}.json"),
                items,
                new Progress<double>(p => progress.Report(offset + (p / providers.Count))),
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Synced catch-up for {Provider}: {Report}", profile.Name, report);
            total = new SyncReport(total.Added + report.Added, total.Updated + report.Updated, total.Removed + report.Removed, total.Unchanged + report.Unchanged);
        }

        return total;
    }
}
