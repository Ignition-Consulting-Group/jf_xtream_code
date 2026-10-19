using System;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.LiveTv;
using Microsoft.Extensions.Logging.Abstractions;

namespace XtreamPro.UnitTests.LiveTv;

public class LiveCatalogTests
{
    private readonly FakeProvider _provider = new();
    private readonly LiveCatalog _catalog;

    public LiveCatalogTests()
    {
        _catalog = new LiveCatalog(_provider.Settings, _provider.Clients, new GuideStore(System.IO.Path.GetTempPath()), NullLogger<LiveCatalog>.Instance);
    }

    private int ApiCalls => _provider.Handler.Requests.Count;

    [Fact]
    public async Task GetChannels_MapsEveryEnabledProvider()
    {
        var channels = await _catalog.GetChannelsAsync(false, TestContext.Current.CancellationToken);

        channels.Count.ShouldBe(3);
        channels.ShouldAllBe(c => c.Id.StartsWith(ChannelId.TunerHostId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetChannels_WithCache_DoesNotRefetch()
    {
        await _catalog.GetChannelsAsync(true, TestContext.Current.CancellationToken);
        var calls = ApiCalls;

        await _catalog.GetChannelsAsync(true, TestContext.Current.CancellationToken);

        ApiCalls.ShouldBe(calls);
    }

    [Fact]
    public async Task GetChannels_WithoutCache_Refetches()
    {
        await _catalog.GetChannelsAsync(true, TestContext.Current.CancellationToken);
        var calls = ApiCalls;

        await _catalog.GetChannelsAsync(false, TestContext.Current.CancellationToken);

        ApiCalls.ShouldBeGreaterThan(calls);
    }

    [Fact]
    public async Task GetChannels_SkipsProvidersWithLiveTvDisabled()
    {
        _provider.Profile.EnableLiveTv = false;

        (await _catalog.GetChannelsAsync(false, TestContext.Current.CancellationToken)).ShouldBeEmpty();
        ApiCalls.ShouldBe(0);
    }

    [Fact]
    public async Task GetChannels_ProviderFailure_IsSkipped()
    {
        _provider.Configuration.Providers.Add(new() { Name = "Broken", Url = "not a url" });

        (await _catalog.GetChannelsAsync(false, TestContext.Current.CancellationToken)).Count.ShouldBe(3);
    }

    [Fact]
    public async Task Find_ResolvesAChannelIdToItsProviderAndStream()
    {
        var found = await _catalog.FindAsync(ChannelId.Format(_provider.Profile.Id, 102), TestContext.Current.CancellationToken);

        found.ShouldNotBeNull();
        found.Value.Profile.ShouldBeSameAs(_provider.Profile);
        found.Value.Channel.StreamId.ShouldBe(102);
    }

    [Fact]
    public async Task EpgOverrides_ReplaceTheProviderGuideId()
    {
        _provider.Profile.EpgOverrides.Add(new() { StreamId = 102, EpgChannelId = "news.one.uk" });

        var streams = await _catalog.GetStreamsAsync(_provider.Profile, TestContext.Current.CancellationToken);
        var channels = await _catalog.GetChannelsAsync(true, TestContext.Current.CancellationToken);

        streams.Single(s => s.StreamId == 102).EpgChannelId.ShouldBe("news.one.uk");
        channels.Single(c => c.Id == ChannelId.Format(_provider.Profile.Id, 102)).TunerChannelId.ShouldBe("news.one.uk");
    }

    [Theory]
    [InlineData("m3u_123")]
    [InlineData("xtreampro_11111111222233334444555555555555_999")]
    [InlineData("xtreampro_99999999222233334444555555555555_101")]
    public async Task Find_UnknownChannel_IsNull(string id)
    {
        (await _catalog.FindAsync(id, TestContext.Current.CancellationToken)).ShouldBeNull();
    }
}
