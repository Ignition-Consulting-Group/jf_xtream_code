using System;
using System.IO;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace XtreamPro.IntegrationTests.Jellyfin;

public sealed class JellyfinFixture : IAsyncLifetime
{
    public const string Image = "jellyfin/jellyfin:12.2.20261005-225228";

    private readonly IContainer _container = new ContainerBuilder(Image)
        .WithResourceMapping(new DirectoryInfo(PluginOutputDirectory()), "/config/plugins/XtreamPro/")
        .WithPortBinding(8096, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged(
            "Main: Startup complete",
            o => o.WithTimeout(TimeSpan.FromMinutes(2))))
        .Build();

    public Uri BaseAddress => new UriBuilder("http", _container.Hostname, _container.GetMappedPublicPort(8096)).Uri;

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    private static string PluginOutputDirectory()
    {
        var testOutput = new DirectoryInfo(AppContext.BaseDirectory);
        var configuration = testOutput.Parent!.Name;
        var repoRoot = testOutput.Parent.Parent!.Parent!.Parent!.Parent!.FullName;
        return Path.Combine(repoRoot, "src", "Jellyfin.Plugin.XtreamPro", "bin", configuration, "net10.0");
    }
}
