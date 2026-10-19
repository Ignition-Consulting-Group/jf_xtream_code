using Jellyfin.Plugin.XtreamPro.Api;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.XtreamPro;

/// <summary>
/// Registers the plugin's services with Jellyfin.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton(new XtreamResilience(new XtreamResilienceOptions()));
        serviceCollection.AddSingleton<ConnectionTracker>();
        serviceCollection.AddSingleton<XtreamClientFactory>();
        serviceCollection
            .AddHttpClient(XtreamClientFactory.HttpClientName)
            .AddHttpMessageHandler(sp => new ResilienceHandler(sp.GetRequiredService<XtreamResilience>()));
    }
}
