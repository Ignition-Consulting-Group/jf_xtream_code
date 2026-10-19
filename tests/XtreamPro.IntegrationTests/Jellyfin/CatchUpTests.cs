using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace XtreamPro.IntegrationTests.Jellyfin;

[Trait("Category", "Container")]
public sealed class CatchUpTests : IClassFixture<JellyfinFixture>, IAsyncLifetime
{
    private const string PluginId = "d7920906-a306-4216-911b-39474ca7596d";

    private readonly JellyfinFixture _jellyfin;
    private readonly JellyfinApi _api;

    public CatchUpTests(JellyfinFixture jellyfin)
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
            ["EnableVod"] = false,
            ["EnableSeries"] = false,
        });
        config["CatchUpPath"] = "/media/xtream/catchup";
        config["StreamBaseUrl"] = "http://localhost:8096";
        await _api.PostAsync($"/Plugins/{PluginId}/Configuration", config, Ct);

        await _api.RunTaskToCompletionAsync("XtreamProRefreshGuide", Ct);
        await _api.RunTaskToCompletionAsync("XtreamProSyncCatchUp", Ct);
        await _api.PostAsync("/Library/VirtualFolders?name=Catch-up&collectionType=tvshows&paths=%2Fmedia%2Fxtream%2Fcatchup&refreshLibrary=true", new { LibraryOptions = new { } }, Ct);
    }

    public ValueTask DisposeAsync()
    {
        _api.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task PastProgrammes_AppearAsEpisodesAndPlayThroughTimeshift()
    {
        var episodes = await JellyfinApi.EventuallyAsync(
            async () => (await _api.GetAsync($"/Items?userId={_api.UserId}&includeItemTypes=Episode&recursive=true&fields=Path", Ct))["Items"]!.AsArray(),
            items => items.Count > 0,
            Ct);
        var shows = (await _api.GetAsync($"/Items?userId={_api.UserId}&includeItemTypes=Series&recursive=true", Ct))["Items"]!.AsArray();

        shows.Select(s => s!["Name"]!.GetValue<string>()).ShouldContain("Fake Channel 1");
        shows.Select(s => s!["Name"]!.GetValue<string>()).ShouldNotContain("Fake Channel 2");
        episodes[0]!["Name"]!.GetValue<string>().ShouldStartWith("Fake Channel");
        await Task.Delay(TimeSpan.FromSeconds(15), Ct);
        (await _jellyfin.FakeProviderLogAsync()).ShouldNotContain("/timeshift/");

        var path = episodes[0]!["Path"]!.GetValue<string>();
        var playback = await _api.PostAsync($"/Items/{episodes[0]!["Id"]!.GetValue<string>()}/PlaybackInfo?userId={_api.UserId}", new { DeviceProfile = new { } }, Ct);
        var url = playback["MediaSources"]!.AsArray()[0]!["Path"]!.GetValue<string>();
        url.ShouldContain("/XtreamPro/Stream/");
        url.ShouldContain("/catchup/");
        path.ShouldEndWith(".strm");

        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = _jellyfin.BaseAddress };
        using var response = await client.GetAsync(new Uri(url).PathAndQuery, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldStartWith(JellyfinFixture.FakeProviderUrl + "/timeshift/demo/demo/60/");
    }
}
