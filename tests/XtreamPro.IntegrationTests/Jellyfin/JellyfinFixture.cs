using System;
using System.IO;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace XtreamPro.IntegrationTests.Jellyfin;

public sealed class JellyfinFixture : IAsyncLifetime
{
    public const string Image = "jellyfin/jellyfin:12.2.20261005-225228";
    public const string FakeProviderUrl = "http://fake-xtream:8080";

    private static readonly string RepoRoot = FindRepoRoot();

    private readonly INetwork _network = new NetworkBuilder().Build();
    private IContainer? _fake;
    private IContainer? _jellyfin;

    public Uri BaseAddress => new UriBuilder("http", _jellyfin!.Hostname, _jellyfin.GetMappedPublicPort(8096)).Uri;

    public async ValueTask InitializeAsync()
    {
        await _network.CreateAsync();

        _fake = new ContainerBuilder("mcr.microsoft.com/dotnet/aspnet:10.0")
            .WithResourceMapping(new DirectoryInfo(OutputDirectory("tools", "FakeXtreamServer")), "/app/")
            .WithEntrypoint("dotnet", "/app/FakeXtreamServer.dll")
            .WithEnvironment("ASPNETCORE_HTTP_PORTS", "8080")
            .WithNetwork(_network)
            .WithNetworkAliases("fake-xtream")
            .WithEnvironment("Fake__MaxConnections", "2")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Application started"))
            .Build();

        _jellyfin = new ContainerBuilder(Image)
            .WithNetwork(_network)
            .WithResourceMapping(new DirectoryInfo(OutputDirectory("src", "Jellyfin.Plugin.XtreamPro")), "/config/plugins/XtreamPro/")
            .WithPortBinding(8096, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged(
                "Main: Startup complete",
                o => o.WithTimeout(TimeSpan.FromMinutes(2))))
            .Build();

        await Task.WhenAll(_fake.StartAsync(), _jellyfin.StartAsync());
    }

    public async ValueTask DisposeAsync()
    {
        if (_jellyfin is not null)
        {
            var (stdout, _) = await _jellyfin.GetLogsAsync();
            await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "jellyfin.log"), stdout);
            await _jellyfin.DisposeAsync();
        }

        if (_fake is not null)
        {
            await _fake.DisposeAsync();
        }

        await _network.DisposeAsync();
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir!.FullName, "Jellyfin.Plugin.XtreamPro.slnx")))
        {
            dir = dir.Parent;
        }

        return dir.FullName;
    }

    private static string OutputDirectory(string folder, string project)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        return Path.Combine(RepoRoot, folder, project, "bin", configuration, "net10.0");
    }
}
