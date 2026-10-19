using System;
using System.IO;
using System.Net;
using System.Net.Http;
using Jellyfin.Plugin.XtreamPro.Api;
using Jellyfin.Plugin.XtreamPro.Configuration;
using Jellyfin.Plugin.XtreamPro.LiveTv;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jellyfin.Plugin.XtreamPro;

/// <summary>
/// Registers the plugin's services with Jellyfin.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.TryAddSingleton(TimeProvider.System);
        serviceCollection.AddSingleton<IXtreamSettings, PluginSettings>();
        serviceCollection.AddSingleton(new XtreamResilience(new XtreamResilienceOptions()));
        serviceCollection.AddSingleton<ConnectionTracker>();
        serviceCollection.AddSingleton<XtreamClientFactory>();
        serviceCollection.AddSingleton<AccountMonitor>();
        serviceCollection
            .AddHttpClient(XtreamClientFactory.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All })
            .AddHttpMessageHandler(sp => new ResilienceHandler(sp.GetRequiredService<XtreamResilience>()));

        serviceCollection.AddSingleton(sp => new GuideStore(Path.Combine(sp.GetRequiredService<IApplicationPaths>().DataPath, "xtreampro")));
        serviceCollection.AddSingleton<LiveCatalog>();
        serviceCollection.AddSingleton<ITunerHost, XtreamTunerHost>();
        serviceCollection.AddSingleton<IListingsProvider, XtreamListingsProvider>();
        serviceCollection.AddHostedService<ListingsRegistration>();
    }
}
