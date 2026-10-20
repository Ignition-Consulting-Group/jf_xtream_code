using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.LiveTv;
using MediaBrowser.Model.Plugins;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.XtreamPro.LiveTv;

/// <summary>
/// Adds the Xtream Pro tuner and guide provider to Jellyfin's Live TV settings, or removes them when Live TV is turned off, so no manual setup is needed.
/// </summary>
public class ListingsRegistration : IHostedService
{
    private const string LiveTvKey = "livetv";

    private readonly IConfigurationManager _config;
    private readonly BasePlugin<PluginConfiguration>? _plugin;
    private readonly ILogger<ListingsRegistration> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListingsRegistration"/> class.
    /// </summary>
    /// <param name="config">Server configuration.</param>
    /// <param name="logger">Logger.</param>
    public ListingsRegistration(IConfigurationManager config, ILogger<ListingsRegistration> logger)
        : this(config, Plugin.Instance, logger)
    {
    }

    internal ListingsRegistration(IConfigurationManager config, BasePlugin<PluginConfiguration>? plugin, ILogger<ListingsRegistration> logger)
    {
        _config = config;
        _plugin = plugin;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Apply(_plugin?.Configuration ?? new PluginConfiguration());
        if (_plugin is not null)
        {
            _plugin.ConfigurationChanged += OnConfigurationChanged;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_plugin is not null)
        {
            _plugin.ConfigurationChanged -= OnConfigurationChanged;
        }

        return Task.CompletedTask;
    }

    private static string NewId() => Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

    private void OnConfigurationChanged(object? sender, BasePluginConfiguration e) => Apply((PluginConfiguration)e);

    private void Apply(PluginConfiguration plugin)
    {
        // Jellyfin's own SaveListingProvider queues a guide refresh, which is not possible this early in startup.
        try
        {
            var options = _config.GetConfiguration<LiveTvOptions>(LiveTvKey);
            var changed = plugin.ShowLiveTv ? Register(options) : Unregister(options);
            if (changed)
            {
                _config.SaveConfiguration(LiveTvKey, options);
            }
        }
#pragma warning disable CA1031 // A failure here must not stop Jellyfin from starting.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Could not update the Xtream Pro guide provider");
        }
    }

    private static bool Register(LiveTvOptions options)
    {
        var changed = false;
        if (!options.ListingProviders.Any(p => p.Type == ChannelId.TunerHostId))
        {
            options.ListingProviders = [.. options.ListingProviders, new ListingsProviderInfo { Id = NewId(), Type = ChannelId.TunerHostId, EnableAllTuners = true }];
            changed = true;
        }

        // Jellyfin only enables Live TV for users, and its guide task, when a tuner is listed. The tuner host itself ignores this entry.
        if (!options.TunerHosts.Any(t => t.Type == ChannelId.TunerHostId))
        {
            options.TunerHosts = [.. options.TunerHosts, new TunerHostInfo { Id = NewId(), Type = ChannelId.TunerHostId, FriendlyName = "Xtream Pro" }];
            changed = true;
        }

        return changed;
    }

    private static bool Unregister(LiveTvOptions options)
    {
        var listings = options.ListingProviders.Where(p => p.Type != ChannelId.TunerHostId).ToArray();
        var tuners = options.TunerHosts.Where(t => t.Type != ChannelId.TunerHostId).ToArray();
        if (listings.Length == options.ListingProviders.Length && tuners.Length == options.TunerHosts.Length)
        {
            return false;
        }

        options.ListingProviders = listings;
        options.TunerHosts = tuners;
        return true;
    }
}
