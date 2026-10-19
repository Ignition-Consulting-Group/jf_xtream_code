using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace XtreamPro.IntegrationTests.Jellyfin;

[Trait("Category", "Container")]
public sealed class LibrarySyncTests : IClassFixture<JellyfinFixture>, IAsyncLifetime
{
    private const string PluginId = "d7920906-a306-4216-911b-39474ca7596d";

    // FakeXtreamServer: 10 movies across 3 categories, one of them adult; 3 series of 2 seasons x 3 episodes.
    private const int ExpectedMovies = 7;
    private const int ExpectedEpisodes = 18;

    private readonly JellyfinFixture _jellyfin;
    private readonly JellyfinApi _api;

    public LibrarySyncTests(JellyfinFixture jellyfin)
    {
        _jellyfin = jellyfin;
        _api = new JellyfinApi(jellyfin.BaseAddress);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await _api.SetUpAsync(Ct);
        var config = await _api.GetAsync($"/Plugins/{PluginId}/Configuration", Ct);
        if (config["Providers"]!.AsArray().Count > 0)
        {
            return;
        }

        config["Providers"] = new JsonArray(new JsonObject
        {
            ["Name"] = "Fake",
            ["Url"] = JellyfinFixture.FakeProviderUrl,
            ["Username"] = "demo",
            ["Password"] = "demo",
            ["EnableLiveTv"] = false,
        });
        config["MoviesPath"] = "/media/xtream/movies";
        config["ShowsPath"] = "/media/xtream/shows";
        config["StreamBaseUrl"] = "http://localhost:8096";
        await _api.PostAsync($"/Plugins/{PluginId}/Configuration", config, Ct);

        await _api.RunTaskToCompletionAsync("XtreamProSyncMovies", Ct);
        await _api.RunTaskToCompletionAsync("XtreamProSyncSeries", Ct);
        await _api.PostAsync("/Library/VirtualFolders?name=Movies&collectionType=movies&paths=%2Fmedia%2Fxtream%2Fmovies&refreshLibrary=true", new { LibraryOptions = new { } }, Ct);
        await _api.PostAsync("/Library/VirtualFolders?name=Shows&collectionType=tvshows&paths=%2Fmedia%2Fxtream%2Fshows&refreshLibrary=true", new { LibraryOptions = new { } }, Ct);
    }

    public ValueTask DisposeAsync()
    {
        _api.Dispose();
        return ValueTask.CompletedTask;
    }

    private Task<JsonArray> ItemsAsync(string type, int expected)
        => JellyfinApi.EventuallyAsync(
            async () => (await _api.GetAsync($"/Items?userId={_api.UserId}&includeItemTypes={type}&recursive=true", Ct))["Items"]!.AsArray(),
            items => items.Count >= expected,
            Ct);

    [Fact]
    public async Task Sync_CreatesMoviesAndShowsJellyfinRecognises()
    {
        var movies = await ItemsAsync("Movie", ExpectedMovies);
        var episodes = await ItemsAsync("Episode", ExpectedEpisodes);
        var series = await ItemsAsync("Series", 3);

        movies.Count.ShouldBe(ExpectedMovies);
        movies.Select(m => m!["Name"]!.GetValue<string>()).ShouldContain("Fake Movie 1");
        movies.ShouldNotContain(m => m!["Name"]!.GetValue<string>().Contains("Adult", StringComparison.Ordinal));
        episodes.Count.ShouldBe(ExpectedEpisodes);
        series.Select(s => s!["Name"]!.GetValue<string>()).ShouldContain("Fake Show 1");
    }

    [Fact]
    public async Task Movie_PlaysThroughTheSignedRedirect()
    {
        var movie = (await ItemsAsync("Movie", ExpectedMovies)).First(m => m!["Name"]!.GetValue<string>() == "Fake Movie 1")!["Id"]!.GetValue<string>();
        var playback = await _api.PostAsync($"/Items/{movie}/PlaybackInfo?userId={_api.UserId}", new { DeviceProfile = new { } }, Ct);
        var path = playback["MediaSources"]!.AsArray()[0]!["Path"]!.GetValue<string>();
        path.ShouldStartWith("http://localhost:8096/XtreamPro/Stream/");
        path.ShouldNotContain("demo");

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path).PathAndQuery);
        using var response = await new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = _jellyfin.BaseAddress }.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldBe(JellyfinFixture.FakeProviderUrl + "/movie/demo/demo/5000.mkv");
    }

    [Fact]
    public async Task SecondSync_WithoutProviderChanges_WritesNothing()
    {
        await ItemsAsync("Movie", ExpectedMovies);
        await _jellyfin.ExecAsync("touch", "/tmp/marker");
        await Task.Delay(1100, Ct);

        await _api.RunTaskToCompletionAsync("XtreamProSyncMovies", Ct);
        await _api.RunTaskToCompletionAsync("XtreamProSyncSeries", Ct);

        var touched = await _jellyfin.ExecAsync("find", "/media/xtream", "-type", "f", "-newer", "/tmp/marker");
        touched.ShouldBeEmpty();
    }
}
