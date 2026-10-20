using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace XtreamPro.IntegrationTests.Jellyfin;

[Trait("Category", "Container")]
public sealed class LiveTvTests : IClassFixture<JellyfinFixture>, IAsyncLifetime
{
    private const string PluginId = "d7920906-a306-4216-911b-39474ca7596d";

    private readonly JellyfinApi _api;

    public LiveTvTests(JellyfinFixture jellyfin)
    {
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
        });
        await _api.PostAsync($"/Plugins/{PluginId}/Configuration", config, Ct);
    }

    public ValueTask DisposeAsync()
    {
        _api.Dispose();
        return ValueTask.CompletedTask;
    }

    private Task<JsonArray> ChannelsAsync()
        => JellyfinApi.EventuallyAsync(
            async () => (await _api.GetAsync($"/LiveTv/Channels?userId={_api.UserId}", Ct))["Items"]!.AsArray(),
            items => items.Count == 6,
            Ct);

    [Fact]
    public async Task LiveTv_IsShownToUsers()
    {
        await ChannelsAsync();

        var views = (await _api.GetAsync($"/UserViews?userId={_api.UserId}", Ct))["Items"]!.AsArray();
        views.Select(v => v!["CollectionType"]?.GetValue<string>()).ShouldContain("livetv");
    }

    [Fact]
    public async Task ChannelsAndGuide_AppearInJellyfin()
    {
        var channels = await ChannelsAsync();
        channels.Count.ShouldBe(6);
        channels.Select(c => c!["Name"]!.GetValue<string>()).ShouldContain("Fake Channel 1");

        var first = channels.First(c => c!["Name"]!.GetValue<string>() == "Fake Channel 1")!["Id"]!.GetValue<string>();
        var programmes = await JellyfinApi.EventuallyAsync(
            async () => (await _api.GetAsync($"/LiveTv/Programs?userId={_api.UserId}&channelIds={first}&maxStartDate={DateTime.UtcNow.AddHours(2):O}&minEndDate={DateTime.UtcNow:O}", Ct))["Items"]!.AsArray(),
            items => items.Count > 0,
            Ct);
        programmes.ShouldNotBeEmpty();
        programmes[0]!["Name"]!.GetValue<string>().ShouldStartWith("Fake Channel 1 at");
    }

    [Fact]
    public async Task Categories_AreBrowsableThenPlayable()
    {
        await ChannelsAsync();
        var channel = (await _api.GetAsync($"/Channels?userId={_api.UserId}", Ct))["Items"]!.AsArray()
            .ShouldHaveSingleItem()!;
        channel["Name"]!.GetValue<string>().ShouldBe("Live TV Categories");
        var channelId = channel["Id"]!.GetValue<string>();

        var categories = (await _api.GetAsync($"/Channels/{channelId}/Items?userId={_api.UserId}", Ct))["Items"]!.AsArray();
        categories.ShouldNotBeEmpty();
        var folderId = categories[0]!["Id"]!.GetValue<string>();

        var items = (await _api.GetAsync($"/Channels/{channelId}/Items?userId={_api.UserId}&folderId={folderId}", Ct))["Items"]!.AsArray();
        items.ShouldNotBeEmpty();

        var playback = await _api.PostAsync(
            $"/Items/{items[0]!["Id"]!.GetValue<string>()}/PlaybackInfo?userId={_api.UserId}",
            new { DeviceProfile = new { } },
            Ct);
        playback["MediaSources"]!.AsArray().ShouldHaveSingleItem()!["Path"]!.GetValue<string>()
            .ShouldStartWith(JellyfinFixture.FakeProviderUrl + "/live/demo/demo/");
    }

    [Fact]
    public async Task Channel_OpensAStreamFromTheProvider()
    {
        var channel = (await ChannelsAsync()).First()!["Id"]!.GetValue<string>();

        var playback = await _api.PostAsync(
            $"/Items/{channel}/PlaybackInfo?userId={_api.UserId}&autoOpenLiveStream=true",
            new { DeviceProfile = new { } },
            Ct);

        var source = playback["MediaSources"]!.AsArray().ShouldHaveSingleItem()!;
        source["Path"]!.GetValue<string>().ShouldStartWith(JellyfinFixture.FakeProviderUrl + "/live/demo/demo/");
        var liveStreamId = source["LiveStreamId"]!.GetValue<string>();
        liveStreamId.ShouldNotBeNullOrEmpty();

        await _api.PostAsync($"/LiveStreams/Close?liveStreamId={liveStreamId}", new { }, Ct);
    }
}
