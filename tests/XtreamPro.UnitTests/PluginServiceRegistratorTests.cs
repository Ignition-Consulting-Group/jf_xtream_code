using System.IO;
using System.Linq;
using Jellyfin.Plugin.XtreamPro;
using Jellyfin.Plugin.XtreamPro.Api;
using Jellyfin.Plugin.XtreamPro.LiveTv;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.LiveTv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace XtreamPro.UnitTests;

public class PluginServiceRegistratorTests
{
    private readonly ServiceProvider _provider;

    public PluginServiceRegistratorTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var paths = Substitute.For<IApplicationPaths>();
        paths.DataPath.Returns(Path.GetTempPath());
        services.AddSingleton(paths);
        services.AddSingleton(Substitute.For<IConfigurationManager>());

        new PluginServiceRegistrator().RegisterServices(services, Substitute.For<IServerApplicationHost>());
        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public void RegisterServices_ProvidesSingletonClientServices()
    {
        _provider.GetRequiredService<XtreamClientFactory>().ShouldNotBeNull();
        _provider.GetRequiredService<ConnectionTracker>().ShouldBeSameAs(_provider.GetRequiredService<ConnectionTracker>());
    }

    [Fact]
    public void RegisterServices_AddsTunerHostAndGuideProvider()
    {
        _provider.GetServices<ITunerHost>().ShouldHaveSingleItem().ShouldBeOfType<XtreamTunerHost>();
        _provider.GetServices<IListingsProvider>().ShouldHaveSingleItem().ShouldBeOfType<XtreamListingsProvider>();
        _provider.GetServices<IHostedService>().OfType<ListingsRegistration>().ShouldHaveSingleItem();
    }

    [Fact]
    public void RegisterServices_AddsLibrarySync()
    {
        _provider.GetRequiredService<Jellyfin.Plugin.XtreamPro.Sync.LibrarySync>().ShouldNotBeNull();
        _provider.GetRequiredService<Jellyfin.Plugin.XtreamPro.CatchUp.CatchUpSync>().ShouldNotBeNull();
    }
}
