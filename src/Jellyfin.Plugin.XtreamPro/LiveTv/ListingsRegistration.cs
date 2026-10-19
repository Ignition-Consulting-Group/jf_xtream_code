using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.LiveTv;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.XtreamPro.LiveTv;

/// <summary>
/// Adds the Xtream Pro guide provider to Jellyfin's Live TV settings on startup, so no manual setup is needed.
/// </summary>
public class ListingsRegistration : IHostedService
{
    private const string LiveTvKey = "livetv";

    private readonly IConfigurationManager _config;
    private readonly ILogger<ListingsRegistration> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListingsRegistration"/> class.
    /// </summary>
    /// <param name="config">Server configuration.</param>
    /// <param name="logger">Logger.</param>
    public ListingsRegistration(IConfigurationManager config, ILogger<ListingsRegistration> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Jellyfin's own SaveListingProvider queues a guide refresh, which is not possible this early in startup.
        try
        {
            var options = _config.GetConfiguration<LiveTvOptions>(LiveTvKey);
            if (!options.ListingProviders.Any(p => p.Type == ChannelId.TunerHostId))
            {
                var info = new ListingsProviderInfo
                {
                    Id = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
                    Type = ChannelId.TunerHostId,
                    EnableAllTuners = true,
                };
                options.ListingProviders = [.. options.ListingProviders, info];
                _config.SaveConfiguration(LiveTvKey, options);
            }
        }
#pragma warning disable CA1031 // A failure here must not stop Jellyfin from starting.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Could not register the Xtream Pro guide provider");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
