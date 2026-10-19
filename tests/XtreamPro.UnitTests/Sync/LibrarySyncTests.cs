using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Sync;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace XtreamPro.UnitTests.Sync;

public sealed class LibrarySyncTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xtreampro-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeProvider _provider = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 3, 4, 0, 0, 0, TimeSpan.Zero));
    private readonly LibrarySync _sync;

    public LibrarySyncTests()
    {
        _provider.Configuration.MoviesPath = Path.Combine(_root, "movies");
        _provider.Configuration.ShowsPath = Path.Combine(_root, "shows");
        _sync = new LibrarySync(_provider.Settings, _provider.Clients, Path.Combine(_root, "data"), () => "http://jf:8096", _time, NullLogger<LibrarySync>.Instance);
    }

    private int SeriesInfoCalls => _provider.Handler.Requests.Count(r => r.RequestUri!.Query.Contains("get_series_info", StringComparison.Ordinal));

    [Fact]
    public async Task Movies_AreWrittenDirectlyIntoTheLibraryFolder()
    {
        var report = await _sync.SyncMoviesAsync(new Progress<double>(), TestContext.Current.CancellationToken);

        report.Added.ShouldBe(3);
        var strm = Path.Combine(_root, "movies", "Fake Movie (2020) [tmdbid-12345]", "Fake Movie (2020).strm");
        (await File.ReadAllTextAsync(strm, TestContext.Current.CancellationToken)).ShouldStartWith("http://jf:8096/XtreamPro/Stream/");
    }

    [Fact]
    public async Task SeveralProviders_TagTheirFoldersSoTheyNeverCollide()
    {
        _provider.Configuration.Providers.Add(new() { Name = "Second", Url = "http://fake.xtream.test", Username = "u", Password = "p" });

        await _sync.SyncMoviesAsync(new Progress<double>(), TestContext.Current.CancellationToken);

        Directory.Exists(Path.Combine(_root, "movies", "Fake Movie (2020) [tmdbid-12345] [Fake]")).ShouldBeTrue();
        Directory.Exists(Path.Combine(_root, "movies", "Fake Movie (2020) [tmdbid-12345] [Second]")).ShouldBeTrue();
    }

    [Fact]
    public async Task Movies_GenerateAndSaveTheSigningSecretOnce()
    {
        await _sync.SyncMoviesAsync(new Progress<double>(), TestContext.Current.CancellationToken);
        var secret = _provider.Configuration.StreamSecret;

        await _sync.SyncMoviesAsync(new Progress<double>(), TestContext.Current.CancellationToken);

        secret.ShouldNotBeNullOrEmpty();
        _provider.Configuration.StreamSecret.ShouldBe(secret);
        _provider.Settings.Received(1).Save();
    }

    [Fact]
    public async Task Movies_WithoutAPath_AreSkipped()
    {
        _provider.Configuration.MoviesPath = string.Empty;

        (await _sync.SyncMoviesAsync(new Progress<double>(), TestContext.Current.CancellationToken)).ShouldBe(new SyncReport(0, 0, 0, 0));
        _provider.Handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Series_AreWrittenWithEpisodes()
    {
        var report = await _sync.SyncSeriesAsync(new Progress<double>(), TestContext.Current.CancellationToken);

        report.Added.ShouldBe(2);
        Directory.GetFiles(Path.Combine(_root, "shows"), "*.strm", SearchOption.AllDirectories).Length.ShouldBe(4);
    }

    [Fact]
    public async Task Series_InfoIsCachedUntilTheRefreshWindowOrAProviderChange()
    {
        await _sync.SyncSeriesAsync(new Progress<double>(), TestContext.Current.CancellationToken);
        SeriesInfoCalls.ShouldBe(2);

        await _sync.SyncSeriesAsync(new Progress<double>(), TestContext.Current.CancellationToken);
        SeriesInfoCalls.ShouldBe(2);

        _time.Advance(TimeSpan.FromDays(8));
        await _sync.SyncSeriesAsync(new Progress<double>(), TestContext.Current.CancellationToken);
        SeriesInfoCalls.ShouldBe(4);
    }

    [Fact]
    public async Task Series_WhoseInfoFails_KeepTheirExistingFiles()
    {
        await _sync.SyncSeriesAsync(new Progress<double>(), TestContext.Current.CancellationToken);
        _time.Advance(TimeSpan.FromDays(8));
        var failing = new FakeProvider();
        failing.Configuration.ShowsPath = _provider.Configuration.ShowsPath;
        failing.Configuration.StreamSecret = _provider.Configuration.StreamSecret;
        var broken = new StubHandler(r => r.RequestUri!.Query.Contains("get_series_info", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : failing.Handler.Respond(r));
        var sync = new LibrarySync(failing.Settings, FakeProvider.ClientsFor(broken), Path.Combine(_root, "data"), () => "http://jf:8096", _time, NullLogger<LibrarySync>.Instance);

        var report = await sync.SyncSeriesAsync(new Progress<double>(), TestContext.Current.CancellationToken);

        report.Removed.ShouldBe(0);
        Directory.GetFiles(Path.Combine(_root, "shows"), "*.strm", SearchOption.AllDirectories).Length.ShouldBe(4);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
