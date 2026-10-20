using System;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.LiveTv;
using Jellyfin.Plugin.XtreamPro.Tasks;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace XtreamPro.UnitTests.LiveTv;

public class ScheduledTaskTests
{
    private readonly LiveTvHarness _h = new();
    private readonly ITaskManager _tasks = Substitute.For<ITaskManager>();
    private readonly IScheduledTask _jellyfinGuideTask = Substitute.For<IScheduledTask>();

    public ScheduledTaskTests()
    {
        _jellyfinGuideTask.Key.Returns("RefreshGuide");
        var worker = Substitute.For<IScheduledTaskWorker>();
        worker.ScheduledTask.Returns(_jellyfinGuideTask);
        _tasks.ScheduledTasks.Returns([worker]);
    }

    [Fact]
    public async Task RefreshGuide_StoresTheGuideAndAsksJellyfinToReloadIt()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 3, 4, 6, 0, 0, TimeSpan.Zero));
        var task = new RefreshGuideTask(_h.Catalog, _h.Guides, _tasks, time, NullLogger<RefreshGuideTask>.Instance);

        await task.ExecuteAsync(new Progress<double>(), TestContext.Current.CancellationToken);

        var guide = await _h.Guides.LoadAsync(_h.Provider.Profile.Id, TestContext.Current.CancellationToken);
        guide!.Programmes[_h.Channel(101)].ShouldNotBeEmpty();
        _tasks.Received(1).QueueScheduledTask(_jellyfinGuideTask, Arg.Any<TaskOptions>());
        _tasks.Received(1).QueueScheduledTask<PrepareCategoriesTask>();
    }

    [Fact]
    public async Task RefreshChannels_RefetchesAndQueuesTheJellyfinGuide()
    {
        await _h.Catalog.GetChannelsAsync(true, TestContext.Current.CancellationToken);
        var calls = _h.Provider.Handler.Requests.Count;

        await new RefreshChannelsTask(_h.Catalog, _tasks).ExecuteAsync(new Progress<double>(), TestContext.Current.CancellationToken);

        _h.Provider.Handler.Requests.Count.ShouldBeGreaterThan(calls);
        _tasks.Received(1).QueueScheduledTask(_jellyfinGuideTask, Arg.Any<TaskOptions>());
        _tasks.Received(1).QueueScheduledTask<PrepareCategoriesTask>();
    }

    [Fact]
    public async Task AccountCheck_RecordsAccountStatus()
    {
        await new AccountCheckTask(_h.Provider.Settings, _h.Accounts, NullLogger<AccountCheckTask>.Instance)
            .ExecuteAsync(new Progress<double>(), TestContext.Current.CancellationToken);

        _h.Accounts.Last(_h.Provider.Profile.Id)!.UserInfo.Status.ShouldBe("Active");
    }

    [Fact]
    public void Tasks_HaveDistinctKeysAndDefaultTriggers()
    {
        IScheduledTask[] all =
        [
            new RefreshGuideTask(_h.Catalog, _h.Guides, _tasks, TimeProvider.System, NullLogger<RefreshGuideTask>.Instance),
            new RefreshChannelsTask(_h.Catalog, _tasks),
            new AccountCheckTask(_h.Provider.Settings, _h.Accounts, NullLogger<AccountCheckTask>.Instance),
        ];

        all.Select(t => t.Key).Distinct().Count().ShouldBe(3);
        all.ShouldAllBe(t => t.GetDefaultTriggers().Any() && t.Category == "Xtream Pro");
    }
}
