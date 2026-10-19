using Jellyfin.Plugin.XtreamPro;
using Jellyfin.Plugin.XtreamPro.Api;
using MediaBrowser.Controller;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace XtreamPro.UnitTests;

public class PluginServiceRegistratorTests
{
    [Fact]
    public void RegisterServices_ResolvesTheClientFactoryAndTracker()
    {
        var services = new ServiceCollection();

        new PluginServiceRegistrator().RegisterServices(services, Substitute.For<IServerApplicationHost>());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<XtreamClientFactory>().ShouldNotBeNull();
        provider.GetRequiredService<ConnectionTracker>().ShouldBeSameAs(provider.GetRequiredService<ConnectionTracker>());
    }
}
