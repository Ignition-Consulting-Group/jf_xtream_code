using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using XtreamPro.IntegrationTests.Jellyfin;

namespace XtreamPro.UiTests;

[Trait("Category", "Container")]
public partial class ConfigPageTests : IClassFixture<ConfigPageFixture>
{
    private readonly ConfigPageFixture _fixture;

    public ConfigPageTests(ConfigPageFixture fixture)
    {
        _fixture = fixture;
    }

    private Task<System.Text.Json.Nodes.JsonNode> ConfigAsync()
        => _fixture.Api.GetAsync($"/Plugins/{ConfigPageFixture.PluginId}/Configuration", TestContext.Current.CancellationToken);

    private static ILocator Tab(IPage page, string name) => page.GetByRole(AriaRole.Tab, new() { Name = name });

    private static async Task SaveAsync(IPage page)
    {
        await page.RunAndWaitForResponseAsync(
            () => page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync(),
            r => r.Url.Contains("/Configuration", StringComparison.Ordinal) && r.Request.Method == "POST");
    }

    [Fact]
    public async Task AddProvider_TestConnectionShowsAccountStatus_AndSaves()
    {
        var page = await _fixture.OpenConfigPageAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Add provider" }).ClickAsync();
        var card = page.GetByRole(AriaRole.Group).Last;
        await card.GetByLabel("Name", new() { Exact = true }).FillAsync("Added in UI");
        await card.GetByLabel("Server URL").FillAsync(JellyfinFixture.FakeProviderUrl);
        await card.GetByLabel("Username", new() { Exact = true }).FillAsync("demo");
        await card.GetByLabel("Password").FillAsync("demo");
        await card.GetByRole(AriaRole.Button, new() { Name = "Test connection" }).ClickAsync();

        await Assertions.Expect(card.GetByRole(AriaRole.Status)).ToContainTextAsync("Active");
        await Assertions.Expect(card.GetByRole(AriaRole.Status)).ToContainTextAsync("of 2");

        await SaveAsync(page);
        (await ConfigAsync())["Providers"]!.AsArray().Select(p => p!["Name"]!.GetValue<string>()).ShouldContain("Added in UI");
    }

    [Fact]
    public async Task AddProvider_WorksOverPlainHttp()
    {
        // Browsers only expose crypto.randomUUID on secure origins, and most servers are opened at http://<lan address>.
        var page = await _fixture.OpenConfigPageAsync();
        await page.Context.AddInitScriptAsync("delete Crypto.prototype.randomUUID;");
        await page.ReloadAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Add provider" }).ClickAsync();
        await page.GetByRole(AriaRole.Group).Last.GetByLabel("Name", new() { Exact = true }).FillAsync("Plain HTTP");
        await SaveAsync(page);

        var saved = (await ConfigAsync())["Providers"]!.AsArray().Single(p => p!["Name"]!.GetValue<string>() == "Plain HTTP")!;
        Guid.Parse(saved["Id"]!.GetValue<string>()).ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task WrongPassword_ShowsTheProviderError()
    {
        var page = await _fixture.OpenConfigPageAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Add provider" }).ClickAsync();
        var card = page.GetByRole(AriaRole.Group).Last;
        await card.GetByLabel("Server URL").FillAsync(JellyfinFixture.FakeProviderUrl);
        await card.GetByLabel("Username", new() { Exact = true }).FillAsync("demo");
        await card.GetByLabel("Password").FillAsync("wrong");
        await card.GetByRole(AriaRole.Button, new() { Name = "Test connection" }).ClickAsync();

        await Assertions.Expect(card.GetByRole(AriaRole.Status)).ToContainTextAsync("rejected the username or password");
    }

    [Fact]
    public async Task CategoryManager_SearchesAndExcludesCategories()
    {
        await _fixture.SaveFakeProviderAsync();
        var page = await _fixture.OpenConfigPageAsync();
        await Tab(page, "Categories").ClickAsync();

        await page.GetByLabel("Type").SelectOptionAsync("live");
        await page.GetByRole(AriaRole.Button, new() { Name = "Load categories" }).ClickAsync();
        await Assertions.Expect(page.GetByText("3 categories.")).ToBeVisibleAsync();

        await page.GetByLabel("Search categories").FillAsync("sport");
        await Assertions.Expect(page.GetByRole(AriaRole.Table, new() { Name = "Categories" }).Locator("tbody tr")).ToHaveCountAsync(1);
        await page.GetByRole(AriaRole.Button, new() { Name = "Exclude shown" }).ClickAsync();
        await page.GetByLabel("Search categories").FillAsync(string.Empty);
        await page.GetByLabel("Rename UK | News").FillAsync("News");
        await page.GetByRole(AriaRole.Button, new() { Name = "Move Kids up" }).ClickAsync();
        await SaveAsync(page);

        var rules = (await ConfigAsync())["Providers"]![0]!["LiveCategories"]!.AsArray();
        rules.Single(r => r!["CategoryId"]!.GetValue<string>() == "2")!["Excluded"]!.GetValue<bool>().ShouldBeTrue();
        rules.Single(r => r!["CategoryId"]!.GetValue<string>() == "1")!["Rename"]!.GetValue<string>().ShouldBe("News");
        rules.Single(r => r!["CategoryId"]!.GetValue<string>() == "3")!["Order"]!.GetValue<int>()
            .ShouldBeLessThan(rules.Single(r => r!["CategoryId"]!.GetValue<string>() == "2")!["Order"]!.GetValue<int>());
    }

    [Fact]
    public async Task TitleCleanup_PreviewsAsYouType()
    {
        var page = await _fixture.OpenConfigPageAsync();
        await Tab(page, "Title cleanup").ClickAsync();
        var preview = page.GetByRole(AriaRole.Table, new() { Name = "Title preview" });

        await Assertions.Expect(preview).ToContainTextAsync("Fake Movie");
        await page.GetByLabel("Cleanup rules (one regular expression per line)").FillAsync(string.Empty);
        await page.GetByLabel("Sample titles").FillAsync("EN | Only Prefix (2021)");

        await Assertions.Expect(preview.Locator("tbody tr").First).ToContainTextAsync("EN | Only Prefix");
        await Assertions.Expect(preview.Locator("tbody tr").First).ToContainTextAsync("2021");
    }

    [Fact]
    public async Task SyncStatus_ListsTasksAndRunsThem()
    {
        await _fixture.SaveFakeProviderAsync();
        var page = await _fixture.OpenConfigPageAsync();
        await Tab(page, "Sync status").ClickAsync();
        var table = page.GetByRole(AriaRole.Table, new() { Name = "Sync status" });

        await Assertions.Expect(table).ToContainTextAsync("Sync Xtream movies");
        await table.GetByRole(AriaRole.Button, new() { Name = "Run Check Xtream accounts now" }).ClickAsync();

        await Assertions.Expect(table.GetByRole(AriaRole.Row).Filter(new() { HasText = "Check Xtream accounts" })).ToContainTextAsync("Completed", new() { Timeout = 20_000 });
    }

    [Fact]
    public async Task GuideHealth_ShowsCoverage()
    {
        await _fixture.SaveFakeProviderAsync();
        await _fixture.Api.RunTaskToCompletionAsync("XtreamProRefreshGuide", TestContext.Current.CancellationToken);
        var page = await _fixture.OpenConfigPageAsync();

        await Tab(page, "Guide health").ClickAsync();

        await Assertions.Expect(page.Locator("#xp-guide")).ToContainTextAsync(GuideCoverage());
    }

    [Fact]
    public async Task Tabs_WorkFromTheKeyboard()
    {
        var page = await _fixture.OpenConfigPageAsync();

        await Tab(page, "Providers").FocusAsync();
        await page.Keyboard.PressAsync("ArrowRight");

        await Assertions.Expect(Tab(page, "Libraries")).ToHaveAttributeAsync("aria-selected", "true");
        await Assertions.Expect(Tab(page, "Libraries")).ToBeFocusedAsync();
        await Assertions.Expect(page.GetByLabel("Movies folder")).ToBeVisibleAsync();
    }

    [Theory]
    [InlineData(ColorScheme.Dark)]
    [InlineData(ColorScheme.Light)]
    public async Task Page_UsesTheDashboardTextColour(ColorScheme scheme)
    {
        var page = await _fixture.OpenConfigPageAsync(scheme);

        var colours = await page.EvaluateAsync<string[]>(
            "() => [getComputedStyle(document.querySelector('#XtreamProConfigPage .sectionTitle')).color, getComputedStyle(document.querySelector('#XtreamProConfigPage .content-primary')).color]");

        colours[0].ShouldBe(colours[1]);
    }

    [Theory]
    [InlineData(ColorScheme.Dark)]
    [InlineData(ColorScheme.Light)]
    public async Task Buttons_HaveReadableText(ColorScheme scheme)
    {
        var page = await _fixture.OpenConfigPageAsync(scheme);

        var unreadable = await page.EvaluateAsync<string[]>(
            "() => [...document.querySelectorAll('#XtreamProConfigPage button')].filter(b => b.offsetParent).map(b => [b, getComputedStyle(b)]).filter(([, s]) => s.color === s.backgroundColor).map(([b]) => b.textContent.trim())");

        unreadable.ShouldBeEmpty();
    }

    [GeneratedRegex(@"\d+ of 6 channels have guide data\.")]
    private static partial Regex GuideCoverage();
}
