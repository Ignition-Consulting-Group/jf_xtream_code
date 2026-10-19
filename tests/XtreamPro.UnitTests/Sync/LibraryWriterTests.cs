using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Sync;

namespace XtreamPro.UnitTests.Sync;

public sealed class LibraryWriterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xtreampro-tests", Guid.NewGuid().ToString("N"));
    private readonly string _statePath;
    private readonly LibraryWriter _writer = new(batchSize: 2);

    public LibraryWriterTests()
    {
        _statePath = Path.Combine(_root, "state", "movies.json");
    }

    private string Library => Path.Combine(_root, "library");

    private static SyncItem Item(string key, params (string Path, string Content)[] files)
        => new(key, files.ToDictionary(f => f.Path, f => f.Content));

    private Task<SyncReport> Sync(params SyncItem[] items)
        => _writer.ApplyAsync(Library, _statePath, items, new Progress<double>(), TestContext.Current.CancellationToken);

    private string Read(string path) => File.ReadAllText(Path.Combine(Library, path));

    private DateTime Written(string path) => File.GetLastWriteTimeUtc(Path.Combine(Library, path));

    [Fact]
    public async Task FirstSync_WritesEverything()
    {
        var report = await Sync(Item("movie:1", ("A/A.strm", "url1"), ("A/movie.nfo", "nfo1")), Item("movie:2", ("B/B.strm", "url2")));

        report.ShouldBe(new SyncReport(Added: 2, Updated: 0, Removed: 0, Unchanged: 0));
        Read("A/A.strm").ShouldBe("url1");
        Read("B/B.strm").ShouldBe("url2");
    }

    [Fact]
    public async Task SecondSync_WithoutChanges_TouchesNothing()
    {
        await Sync(Item("movie:1", ("A/A.strm", "url1")));
        var before = Written("A/A.strm");
        await Task.Delay(20, TestContext.Current.CancellationToken);

        var report = await Sync(Item("movie:1", ("A/A.strm", "url1")));

        report.ShouldBe(new SyncReport(0, 0, 0, 1));
        Written("A/A.strm").ShouldBe(before);
    }

    [Fact]
    public async Task ChangedItem_RewritesOnlyItsChangedFiles()
    {
        await Sync(Item("movie:1", ("A/A.strm", "url1"), ("A/movie.nfo", "nfo1")));
        var strmBefore = Written("A/A.strm");
        await Task.Delay(20, TestContext.Current.CancellationToken);

        var report = await Sync(Item("movie:1", ("A/A.strm", "url1"), ("A/movie.nfo", "nfo2")));

        report.Updated.ShouldBe(1);
        Read("A/movie.nfo").ShouldBe("nfo2");
        Written("A/A.strm").ShouldBe(strmBefore);
    }

    [Fact]
    public async Task RenamedItem_MovesItsFiles()
    {
        await Sync(Item("movie:1", ("Old/Old.strm", "url")));

        await Sync(Item("movie:1", ("New/New.strm", "url")));

        File.Exists(Path.Combine(Library, "Old/Old.strm")).ShouldBeFalse();
        Directory.Exists(Path.Combine(Library, "Old")).ShouldBeFalse();
        Read("New/New.strm").ShouldBe("url");
    }

    [Fact]
    public async Task RemovedItem_IsDeletedWithItsEmptyFolders()
    {
        await Sync(Item("series:1", ("Show/Season 01/E1.strm", "u")), Item("movie:2", ("B/B.strm", "u")));

        var report = await Sync(Item("movie:2", ("B/B.strm", "u")));

        report.Removed.ShouldBe(1);
        Directory.Exists(Path.Combine(Library, "Show")).ShouldBeFalse();
        Directory.Exists(Library).ShouldBeTrue();
    }

    [Fact]
    public async Task PathTakenOverByAnotherItem_IsKept()
    {
        await Sync(Item("movie:1", ("A/A.strm", "old")));

        await Sync(Item("movie:3", ("A/A.strm", "new")));

        Read("A/A.strm").ShouldBe("new");
    }

    [Fact]
    public async Task KeptItems_SurviveEvenWhenNotPlanned()
    {
        await Sync(Item("series:1", ("S/tvshow.nfo", "x")), Item("series:2", ("T/tvshow.nfo", "y")));

        var report = await _writer.ApplyAsync(Library, _statePath, [Item("series:2", ("T/tvshow.nfo", "y"))], new Progress<double>(), TestContext.Current.CancellationToken, new HashSet<string> { "series:1" });
        await Sync(Item("series:2", ("T/tvshow.nfo", "y")), Item("series:1", ("S/tvshow.nfo", "x")));

        report.Removed.ShouldBe(0);
        File.Exists(Path.Combine(Library, "S/tvshow.nfo")).ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyCatalogue_DoesNotWipeTheLibrary()
    {
        await Sync(Item("movie:1", ("A/A.strm", "u")));

        var report = await Sync();

        report.Removed.ShouldBe(0);
        File.Exists(Path.Combine(Library, "A/A.strm")).ShouldBeTrue();
    }

    [Fact]
    public async Task FilesTheUserDeleted_AreWrittenAgain()
    {
        await Sync(Item("movie:1", ("A/A.strm", "u")));
        File.Delete(Path.Combine(Library, "A/A.strm"));

        await Sync(Item("movie:1", ("A/A.strm", "u")));

        File.Exists(Path.Combine(Library, "A/A.strm")).ShouldBeTrue();
    }

    [Fact]
    public async Task PathsOutsideTheLibrary_AreRejected()
    {
        await Should.ThrowAsync<InvalidOperationException>(() => Sync(Item("movie:1", ("../escape.strm", "u"))));
    }

    [Fact]
    public async Task Progress_IsReportedPerBatch()
    {
        var reports = new List<double>();
        var progress = new SynchronousProgress(reports.Add);

        await _writer.ApplyAsync(Library, _statePath, Enumerable.Range(1, 5).Select(i => Item($"m:{i}", ($"{i}/{i}.strm", "u"))).ToList(), progress, TestContext.Current.CancellationToken);

        reports.Count.ShouldBeGreaterThanOrEqualTo(3);
        reports[^1].ShouldBe(100);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private sealed class SynchronousProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
