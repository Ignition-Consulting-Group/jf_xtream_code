using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Configuration;
using Jellyfin.Plugin.XtreamPro.LiveTv;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.MediaInfo;

namespace XtreamPro.UnitTests.LiveTv;

public class CategoryChannelTests
{
    private readonly LiveTvHarness _h = new();

    private CategoryChannel Channel() => new(_h.Catalog);

    private async Task<ChannelItemResult> ItemsAsync(string? folderId = null)
        => await Channel().GetChannelItems(new InternalChannelItemQuery { FolderId = folderId! }, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Root_ListsCategoriesInChannelOrder()
    {
        var root = await ItemsAsync();

        root.Items.Select(i => i.Name).ShouldBe(["UK | News", "Sports"]);
        root.Items.ShouldAllBe(i => i.Type == ChannelItemType.Folder);
    }

    [Fact]
    public async Task Root_UsesCategoryRenamesAndExcludes()
    {
        _h.Provider.Profile.LiveCategories.Add(new CategoryRule { CategoryId = "1", Excluded = true });
        _h.Provider.Profile.LiveCategories.Add(new CategoryRule { CategoryId = "2", Rename = "Sport" });

        (await ItemsAsync()).Items.Select(i => i.Name).ShouldBe(["Sport"]);
    }

    [Fact]
    public async Task Category_ListsItsChannelsAsLiveStreams()
    {
        var sports = (await ItemsAsync()).Items.Single(i => i.Name == "Sports");

        var channels = (await ItemsAsync(sports.Id)).Items;

        channels.Select(c => c.Name).ShouldBe(["2 Sports Two", "3 Odd Casing"]);
        channels.ShouldAllBe(c => c.Type == ChannelItemType.Media && c.IsLiveStream && c.MediaType == ChannelMediaType.Video);
        channels[0].Id.ShouldBe(_h.Channel(102));
    }

    [Fact]
    public async Task Category_CarriesTheChannelLogo()
    {
        var news = (await ItemsAsync()).Items[0];

        (await ItemsAsync(news.Id)).Items.ShouldHaveSingleItem().ImageUrl.ShouldBe("http://fake.xtream.test/logos/101.png");
    }

    [Fact]
    public async Task Root_WithSeveralProviders_NamesTheProvider()
    {
        _h.Provider.Configuration.Providers.Add(new ProviderProfile { Name = "Second", Url = "http://fake.xtream.test", Username = "demo", Password = "demo" });

        (await ItemsAsync()).Items.Select(i => i.Name).ShouldBe(["Fake - UK | News", "Fake - Sports", "Second - UK | News", "Second - Sports"]);
    }

    [Fact]
    public async Task MediaInfo_PlaysTheProviderStreamThroughTheServer()
    {
        var source = (await Channel().GetChannelItemMediaInfo(_h.Channel(101), TestContext.Current.CancellationToken)).ShouldHaveSingleItem();

        source.ShouldSatisfyAllConditions(
            s => s.Path.ShouldBe("http://fake.xtream.test/live/demo/demo/101.ts"),
            s => s.Protocol.ShouldBe(MediaProtocol.Http),
            s => s.IsInfiniteStream.ShouldBeTrue(),
            s => s.RequiresOpening.ShouldBeFalse(),
            s => s.SupportsDirectPlay.ShouldBeFalse(),
            s => s.RequiredHttpHeaders["User-Agent"].ShouldBe(_h.Provider.Profile.UserAgent));
    }

    [Fact]
    public async Task MediaInfo_ForUnknownIds_IsEmpty()
    {
        (await Channel().GetChannelItemMediaInfo("nope", TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public void IsEnabled_OnlyWithALiveTvProvider()
    {
        Channel().IsEnabledFor("u").ShouldBeTrue();

        _h.Provider.Profile.EnableLiveTv = false;

        Channel().IsEnabledFor("u").ShouldBeFalse();
    }
}
