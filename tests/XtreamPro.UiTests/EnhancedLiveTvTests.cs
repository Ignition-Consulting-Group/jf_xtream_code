using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Playwright;
using XtreamPro.IntegrationTests.Jellyfin;

namespace XtreamPro.UiTests;

[Trait("Category", "Container")]
public sealed class EnhancedLiveTvTests : IClassFixture<ConfigPageFixture>, IAsyncLifetime
{
    private readonly ConfigPageFixture _fixture;
    private readonly System.Collections.Generic.List<IPage> _pages = [];

    public EnhancedLiveTvTests(ConfigPageFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        await _fixture.SaveFakeProviderAsync(enhancedLiveTv: true);
        var ct = TestContext.Current.CancellationToken;
        var channels = await JellyfinApi.EventuallyAsync(
            async () => (await _fixture.Api.GetAsync($"/LiveTv/Channels?userId={_fixture.Api.UserId}", ct))["Items"]!.AsArray(),
            items => items.Count == 6,
            ct);
        if (channels.Count == 6)
        {
            return;
        }

        await _fixture.Api.RunTaskToCompletionAsync("XtreamProRefreshGuide", ct);
        await JellyfinApi.EventuallyAsync(
            async () => (await _fixture.Api.GetAsync($"/LiveTv/Channels?userId={_fixture.Api.UserId}", ct))["Items"]!.AsArray(),
            items => items.Count == 6,
            ct);
    }

    public async ValueTask DisposeAsync()
    {
        // Leaving the page fires pagehide, which closes the open stream so the next test has a free connection.
        foreach (var page in _pages)
        {
            await page.GotoAsync("about:blank");
        }

        await Task.Delay(1000, TestContext.Current.CancellationToken);
        foreach (var page in _pages)
        {
            await page.CloseAsync();
        }
    }

    private async Task<IPage> OpenAsync()
    {
        var page = await _fixture.OpenAsync("/XtreamPro/Web/");
        _pages.Add(page);
        await Assertions.Expect(page.GetByRole(AriaRole.Status).First).ToHaveTextAsync("6 channels");
        return page;
    }

    private static ILocator Options(IPage page) => page.GetByRole(AriaRole.Listbox, new() { Name = "Channel list" }).GetByRole(AriaRole.Option);

    private static ILocator Selected(IPage page) => page.Locator("#channels [aria-selected='true']");

    // Tuning takes a few seconds; leaving before it finishes would hold the provider connection.
    private static Task TunedAsync(IPage page, string channel)
        => Assertions.Expect(page.Locator("#status")).ToHaveTextAsync("Playing " + channel, new() { Timeout = 30_000 });

    [Fact]
    public async Task Categories_FilterTheChannelList()
    {
        var page = await OpenAsync();
        await Assertions.Expect(Options(page)).ToHaveCountAsync(6);

        await page.GetByRole(AriaRole.Navigation, new() { Name = "Categories" }).GetByRole(AriaRole.Button, new() { Name = "Sports" }).ClickAsync();

        await Assertions.Expect(Options(page)).ToHaveCountAsync(2);
        await Assertions.Expect(Options(page).First).ToContainTextAsync("Fake Channel 2");
    }

    [Fact]
    public async Task Categories_KeepTheFullProviderName()
    {
        var page = await OpenAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Navigation, new() { Name = "Categories" }).GetByRole(AriaRole.Button, new() { Name = "UK | News" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ArrowKeys_ZapThroughChannelsAndShowNowNext()
    {
        var page = await OpenAsync();

        await page.Keyboard.PressAsync("ArrowDown");
        await Assertions.Expect(Selected(page)).ToContainTextAsync("Fake Channel 1");
        await page.Keyboard.PressAsync("ArrowDown");
        await Assertions.Expect(Selected(page)).ToContainTextAsync("Fake Channel 2");
        await page.Keyboard.PressAsync("ArrowUp");

        await Assertions.Expect(Selected(page)).ToContainTextAsync("Fake Channel 1");
        await Assertions.Expect(page.Locator("#now")).ToContainTextAsync("Fake Channel 1 at");
        await TunedAsync(page, "Fake Channel 1");
    }

    [Fact]
    public async Task NumberKeys_JumpToAChannel()
    {
        var page = await OpenAsync();

        await page.Keyboard.PressAsync("4");

        await Assertions.Expect(Selected(page)).ToContainTextAsync("Fake Channel 4");
        await Assertions.Expect(page.Locator("#channel-name")).ToHaveTextAsync("4 Fake Channel 4");
        await TunedAsync(page, "Fake Channel 4");
    }

    [Fact]
    public async Task Preview_PlaysThroughJellyfin()
    {
        var page = await OpenAsync();
        var playlist = page.WaitForResponseAsync(r => r.Url.Contains("master.m3u8", StringComparison.Ordinal), new() { Timeout = 60_000 });

        await Options(page).First.ClickAsync();

        (await playlist).Status.ShouldBe((int)HttpStatusCode.OK);
        await TunedAsync(page, "Fake Channel 1");
    }

    [Fact]
    public async Task Favourites_ArePersistedForTheUser()
    {
        var page = await OpenAsync();
        await Options(page).Nth(2).ClickAsync();

        await page.Keyboard.PressAsync("f");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Remove favourite" })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Favourites" }).ClickAsync();

        await Assertions.Expect(Options(page)).ToHaveCountAsync(1);
        await Assertions.Expect(Options(page).First).ToContainTextAsync("Fake Channel 3");
        await TunedAsync(page, "Fake Channel 3");
    }

    [Fact]
    public async Task Guide_ShowsProgrammesForEachChannel()
    {
        var page = await OpenAsync();
        var guide = page.GetByRole(AriaRole.Grid, new() { Name = "Programme guide" });

        await Assertions.Expect(guide.GetByRole(AriaRole.Row)).ToHaveCountAsync(6);
        await Assertions.Expect(guide.GetByRole(AriaRole.Row).First).ToContainTextAsync("Fake Channel 1 at");
    }

    [Fact]
    public async Task Loader_AddsAMenuLinkToTheWebClient()
    {
        var page = await _fixture.OpenAsync("/web/#/home");
        await page.AddScriptTagAsync(new() { Url = "/XtreamPro/Web/inject.js" });

        await page.GetByRole(AriaRole.Button, new() { Name = "Menu" }).ClickAsync();

        await Assertions.Expect(page.Locator("#xtreampro-livetv-link")).ToHaveAttributeAsync("href", "/XtreamPro/Web/");
    }

    [Fact]
    public async Task WithoutFileTransformation_ThePageStillWorksAndTheDashboardExplainsWhy()
    {
        var ct = TestContext.Current.CancellationToken;
        await _fixture.Api.RunTaskAsync("XtreamProRegisterEnhancedUi", ct);
        var status = await JellyfinApi.EventuallyAsync(
            () => _fixture.Api.GetAsync("/XtreamPro/EnhancedUi", ct),
            s => s["Message"]?.GetValue<string>()?.Contains("/XtreamPro/Web/", StringComparison.Ordinal) == true,
            ct);

        status["Injected"]!.GetValue<bool>().ShouldBeFalse();
        status["Message"]!.GetValue<string>().ShouldContain("/XtreamPro/Web/");
        await OpenAsync();
    }

    [Fact]
    public async Task Loader_OnAPageWithoutTheMenu_DoesNothing()
    {
        var page = await _fixture.OpenAsync("/XtreamPro/Web/");
        var errors = new System.Collections.Generic.List<string>();
        page.PageError += (_, e) => errors.Add(e);

        await page.AddScriptTagAsync(new() { Url = "/XtreamPro/Web/inject.js" });
        await page.WaitForTimeoutAsync(500);

        errors.ShouldBeEmpty();
        (await page.Locator("#xtreampro-livetv-link").CountAsync()).ShouldBe(0);
    }
}
