using System;
using System.IO;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro;
using Jellyfin.Plugin.XtreamPro.Configuration;
using Jellyfin.Plugin.XtreamPro.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using NSubstitute;

namespace XtreamPro.UnitTests.LiveTv;

public class RefreshOnSaveTests
{
    private readonly ITaskManager _tasks = Substitute.For<ITaskManager>();
    private readonly Plugin _plugin;

    public RefreshOnSaveTests()
    {
        var paths = Substitute.For<IApplicationPaths>();
        paths.PluginConfigurationsPath.Returns(Path.GetTempPath());
        _plugin = new Plugin(paths, Substitute.For<IXmlSerializer>());
        _plugin.UpdateConfiguration(new PluginConfiguration());
    }

    private async Task<RefreshOnSave> StartAsync()
    {
        var service = new RefreshOnSave(_tasks, _plugin);
        await service.StartAsync(TestContext.Current.CancellationToken);
        return service;
    }

    private static PluginConfiguration WithProvider(string url)
        => new() { Providers = [new ProviderProfile { Id = Guid.Parse("11111111-2222-3333-4444-555555555555"), Url = url }] };

    [Fact]
    public async Task SavingChangedProviders_RefreshesTheGuide()
    {
        await StartAsync();

        _plugin.UpdateConfiguration(WithProvider("http://fake.example"));

        _tasks.Received(1).QueueScheduledTask<RefreshGuideTask>();
    }

    [Fact]
    public async Task SavingOtherSettings_LeavesTheGuideAlone()
    {
        _plugin.UpdateConfiguration(WithProvider("http://fake.example"));
        await StartAsync();

        var config = WithProvider("http://fake.example");
        config.MoviesPath = "/media/movies";
        _plugin.UpdateConfiguration(config);

        _tasks.DidNotReceive().QueueScheduledTask<RefreshGuideTask>();
    }

    [Fact]
    public async Task AfterStop_SavingDoesNothing()
    {
        var service = await StartAsync();
        await service.StopAsync(TestContext.Current.CancellationToken);

        _plugin.UpdateConfiguration(WithProvider("http://fake.example"));

        _tasks.DidNotReceive().QueueScheduledTask<RefreshGuideTask>();
    }
}
