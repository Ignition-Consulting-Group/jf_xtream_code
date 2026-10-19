using System;
using System.IO;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Sync;
using Jellyfin.Plugin.XtreamPro.Tasks;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace XtreamPro.UnitTests.Sync;

public sealed class SyncTaskTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xtreampro-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeProvider _provider = new();
    private readonly ILibraryManager _library = Substitute.For<ILibraryManager>();
    private readonly LibrarySync _sync;

    public SyncTaskTests()
    {
        _provider.Configuration.MoviesPath = Path.Combine(_root, "movies");
        _provider.Configuration.ShowsPath = Path.Combine(_root, "shows");
        _sync = new LibrarySync(_provider.Settings, _provider.Clients, Path.Combine(_root, "data"), () => "http://jf", TimeProvider.System, NullLogger<LibrarySync>.Instance);
    }

    [Fact]
    public async Task MovieSync_QueuesALibraryScanWhenFilesChanged()
    {
        var task = new SyncMoviesTask(_sync, _library, Status);

        await task.ExecuteAsync(new Progress<double>(), TestContext.Current.CancellationToken);
        await task.ExecuteAsync(new Progress<double>(), TestContext.Current.CancellationToken);

        _library.Received(1).QueueLibraryScan();
    }

    [Fact]
    public async Task SeriesSync_QueuesALibraryScanWhenFilesChanged()
    {
        await new SyncSeriesTask(_sync, _library, Status).ExecuteAsync(new Progress<double>(), TestContext.Current.CancellationToken);

        _library.Received(1).QueueLibraryScan();
    }

    [Fact]
    public void Tasks_AreInTheXtreamCategory()
    {
        new SyncMoviesTask(_sync, _library, Status).Category.ShouldBe("Xtream Pro");
        new SyncSeriesTask(_sync, _library, Status).Key.ShouldBe("XtreamProSyncSeries");
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
