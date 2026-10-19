using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Api;
using Jellyfin.Plugin.XtreamPro.Configuration;
using Jellyfin.Plugin.XtreamPro.LiveTv;
using Jellyfin.Plugin.XtreamPro.Sync;
using Jellyfin.Plugin.XtreamPro.Tasks;
using Jellyfin.Plugin.XtreamPro.Web;
using MediaBrowser.Model.Tasks;
using NSubstitute;
using XtreamPro.UnitTests.LiveTv;

namespace XtreamPro.UnitTests.Web;

public class ConfigControllerTests
{
    private readonly LiveTvHarness _h = new();
    private readonly ITaskManager _tasks = Substitute.For<ITaskManager>();
    private readonly SyncStatusStore _status = new(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "xtreampro-tests", Guid.NewGuid().ToString("N"), "s.json"));
    private readonly EnhancedUiStatus _enhancedUi = new() { Message = "Linked from the web client menu.", Injected = true };
    private readonly ConfigController _controller;

    public ConfigControllerTests()
    {
        _controller = new ConfigController(_h.Provider.Clients, _h.Connections, _h.Catalog, _h.Guides, _tasks, _status, _enhancedUi);
    }

    [Fact]
    public async Task TestConnection_ReportsAccountAndConnectionUsage()
    {
        _h.Connections.TryAcquire(_h.Provider.Profile.Id, null, out _);

        var result = await _controller.TestConnection(_h.Provider.Profile, TestContext.Current.CancellationToken);

        result.ShouldBe(new ConnectionTestResult(true, "Active", DateTimeOffset.FromUnixTimeSeconds(1798761600), false, 2, 1, 1));
    }

    [Fact]
    public async Task TestConnection_ExplainsFailures()
    {
        var bad = await _controller.TestConnection(new ProviderProfile { Url = "nope" }, TestContext.Current.CancellationToken);
        var refused = await new ConfigController(FakeProvider.ClientsFor(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway))), _h.Connections, _h.Catalog, _h.Guides, _tasks, _status, _enhancedUi)
            .TestConnection(_h.Provider.Profile, TestContext.Current.CancellationToken);
        var rejected = await new ConfigController(FakeProvider.ClientsFor(StubHandler.ForFixture("account_info_auth_failed.json")), _h.Connections, _h.Catalog, _h.Guides, _tasks, _status, _enhancedUi)
            .TestConnection(_h.Provider.Profile, TestContext.Current.CancellationToken);

        bad.Ok.ShouldBeFalse();
        bad.Error.ShouldNotBeNullOrEmpty();
        refused.Ok.ShouldBeFalse();
        refused.Error.ShouldNotBeNull().ShouldContain("502");
        rejected.ShouldBe(new ConnectionTestResult(false, Error: "The provider rejected the username or password."));
    }

    [Theory]
    [InlineData("live", "UK | News")]
    [InlineData("vod", "Movies | Drama")]
    [InlineData("series", "Shows")]
    public async Task Categories_ListTheProviderCategoriesForAType(string type, string first)
    {
        var categories = await _controller.Categories(type, _h.Provider.Profile, TestContext.Current.CancellationToken);

        categories.Value!.First().Name.ShouldBe(first);
    }

    [Fact]
    public async Task Categories_UnknownType_IsABadRequest()
    {
        (await _controller.Categories("radio", _h.Provider.Profile, TestContext.Current.CancellationToken)).Result
            .ShouldBeOfType<Microsoft.AspNetCore.Mvc.BadRequestResult>();
    }

    [Fact]
    public void TitlePreview_AppliesTheGivenRules()
    {
        var preview = _controller.PreviewTitles(new TitlePreviewRequest { Rules = [@"^EN \| "], Titles = ["EN | Film (2020)", "Other"] });

        preview.ShouldBe([new TitlePreview("EN | Film (2020)", "Film", 2020), new TitlePreview("Other", "Other", null)]);
    }

    [Fact]
    public void Status_ListsXtreamTasksWithTheirLastReport()
    {
        var mine = Worker("XtreamProSyncMovies", "Xtream Pro");
        var theirs = Worker("RefreshGuide", "Live TV");
        _tasks.ScheduledTasks.Returns([mine, theirs]);
        _status.Record("XtreamProSyncMovies", new SyncReport(1, 0, 0, 5), DateTimeOffset.UtcNow);

        var status = _controller.Status().ShouldHaveSingleItem();

        status.Key.ShouldBe("XtreamProSyncMovies");
        status.State.ShouldBe("Idle");
        status.LastResult.ShouldBe("Completed");
        status.Report.ShouldBe(new SyncReport(1, 0, 0, 5));
    }

    [Fact]
    public void EnhancedUi_ReportsTheLinkStatus()
    {
        _controller.EnhancedUi().ShouldBeSameAs(_enhancedUi);
    }

    [Fact]
    public async Task GuideHealth_ListsChannelsWithoutGuideData()
    {
        var streams = await _h.Catalog.GetStreamsAsync(_h.Provider.Profile, TestContext.Current.CancellationToken);
        await _h.Guides.SaveAsync(
            _h.Provider.Profile.Id,
            new GuideResult(new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IReadOnlyList<Jellyfin.Plugin.XtreamPro.LiveTv.Xmltv.XmltvProgramme>>(), new System.Collections.Generic.Dictionary<string, string>(), [_h.Channel(102)]),
            TestContext.Current.CancellationToken);

        var health = (await _controller.GuideHealth(TestContext.Current.CancellationToken)).ShouldHaveSingleItem();

        health.Channels.ShouldBe(streams.Count);
        health.Missing.ShouldBe([new MissingGuide(102, "Sports Two", null)]);
    }

    private static IScheduledTaskWorker Worker(string key, string category)
    {
        var task = Substitute.For<IScheduledTask>();
        task.Key.Returns(key);
        var worker = Substitute.For<IScheduledTaskWorker>();
        worker.ScheduledTask.Returns(task);
        worker.Category.Returns(category);
        worker.Id.Returns(key + "-id");
        worker.Name.Returns(key);
        worker.State.Returns(TaskState.Idle);
        worker.LastExecutionResult.Returns(new TaskResult { Status = TaskCompletionStatus.Completed, EndTimeUtc = DateTime.UtcNow });
        return worker;
    }
}
