using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.CatchUp;
using Jellyfin.Plugin.XtreamPro.LiveTv;
using Jellyfin.Plugin.XtreamPro.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using XtreamPro.UnitTests.LiveTv;

namespace XtreamPro.UnitTests.CatchUp;

public sealed class CatchUpSyncTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xtreampro-tests", Guid.NewGuid().ToString("N"));
    private readonly LiveTvHarness _h = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 3, 4, 12, 0, 0, TimeSpan.Zero));

    private CatchUpSync Sync() => new(_h.Provider.Settings, _h.Catalog, _h.Guides, Path.Combine(_root, "data"), () => "http://jf", _time, NullLogger<CatchUpSync>.Instance);

    private async Task BuildGuide()
    {
        var streams = await _h.Catalog.GetStreamsAsync(_h.Provider.Profile, TestContext.Current.CancellationToken);
        var guide = await new GuideBuilder(10, NullLogger.Instance).BuildAsync(_h.Provider.Profile, _h.Catalog.CreateClient(_h.Provider.Profile), [.. streams], _time.GetUtcNow().AddHours(-6), TestContext.Current.CancellationToken);
        await _h.Guides.SaveAsync(_h.Provider.Profile.Id, guide, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Sync_WritesFinishedProgrammesForArchiveChannels()
    {
        _h.Provider.Configuration.CatchUpPath = Path.Combine(_root, "catchup");
        await BuildGuide();

        var report = await Sync().SyncAsync(new Progress<double>(), TestContext.Current.CancellationToken);

        report.Added.ShouldBeGreaterThan(1);
        Directory.GetFiles(Path.Combine(_root, "catchup"), "*.strm", SearchOption.AllDirectories)
            .ShouldAllBe(f => f.Contains("UK News One", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sync_DropsProgrammesThatLeaveTheWindow()
    {
        _h.Provider.Configuration.CatchUpPath = Path.Combine(_root, "catchup");
        await BuildGuide();
        await Sync().SyncAsync(new Progress<double>(), TestContext.Current.CancellationToken);

        _time.Advance(TimeSpan.FromDays(10));
        await Sync().SyncAsync(new Progress<double>(), TestContext.Current.CancellationToken);

        Directory.Exists(Path.Combine(_root, "catchup")).ShouldBeTrue();
        Directory.GetFiles(Path.Combine(_root, "catchup"), "*.strm", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public async Task Task_QueuesALibraryScanWhenProgrammesChanged()
    {
        _h.Provider.Configuration.CatchUpPath = Path.Combine(_root, "catchup");
        await BuildGuide();
        var library = NSubstitute.Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();

        await new Jellyfin.Plugin.XtreamPro.Tasks.SyncCatchUpTask(Sync(), library, Status).ExecuteAsync(new Progress<double>(), TestContext.Current.CancellationToken);

        library.Received(1).QueueLibraryScan();
    }

    [Fact]
    public async Task Sync_WithoutAPath_DoesNothing()
    {
        (await Sync().SyncAsync(new Progress<double>(), TestContext.Current.CancellationToken)).Added.ShouldBe(0);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private static SyncStatusStore Status => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "xtreampro-tests", System.Guid.NewGuid().ToString("N"), "status.json"));
}
