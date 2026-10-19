using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;
using XtreamPro.IntegrationTests.Jellyfin;

namespace XtreamPro.UiTests;

/// <summary>
/// Regenerates the README screenshots: <c>dotnet test --project tests/XtreamPro.UiTests --filter-trait "Category=Screenshots"</c>.
/// </summary>
[Trait("Category", "Screenshots")]
public sealed class ScreenshotTests : IClassFixture<ConfigPageFixture>
{
    private readonly ConfigPageFixture _fixture;

    public ScreenshotTests(ConfigPageFixture fixture)
    {
        _fixture = fixture;
    }

    private static string PathFor(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir!.FullName, "Jellyfin.Plugin.XtreamPro.slnx")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir.FullName, "docs", "screenshots", name);
    }

    [Fact]
    public async Task Capture()
    {
        var ct = TestContext.Current.CancellationToken;
        await _fixture.SaveFakeProviderAsync(enhancedLiveTv: true);
        await _fixture.Api.RunTaskToCompletionAsync("XtreamProRefreshGuide", ct);
        await JellyfinApi.EventuallyAsync(
            async () => (await _fixture.Api.GetAsync($"/LiveTv/Channels?userId={_fixture.Api.UserId}", ct))["Items"]!.AsArray(),
            items => items.Count == 6,
            ct);

        var config = await _fixture.OpenConfigPageAsync();
        await config.SetViewportSizeAsync(1280, 900);
        await config.GetByRole(AriaRole.Button, new() { Name = "Test connection" }).ClickAsync();
        await Assertions.Expect(config.GetByRole(AriaRole.Status).First).ToContainTextAsync("Active");
        await config.ScreenshotAsync(new() { Path = PathFor("dashboard-providers.png"), FullPage = true });

        await config.GetByRole(AriaRole.Tab, new() { Name = "Categories" }).ClickAsync();
        await config.GetByRole(AriaRole.Button, new() { Name = "Load categories" }).ClickAsync();
        await Assertions.Expect(config.GetByText("3 categories.")).ToBeVisibleAsync();
        await config.ScreenshotAsync(new() { Path = PathFor("dashboard-categories.png"), FullPage = true });

        var live = await _fixture.OpenAsync("/XtreamPro/Web/");
        await live.SetViewportSizeAsync(1280, 900);
        await Assertions.Expect(live.Locator("#status")).ToHaveTextAsync("6 channels");
        await live.Keyboard.PressAsync("ArrowDown");
        await Assertions.Expect(live.Locator("#status")).ToHaveTextAsync("Playing Fake Channel 1", new() { Timeout = 30_000 });
        await live.WaitForTimeoutAsync(4000);
        await live.ScreenshotAsync(new() { Path = PathFor("live-tv.png") });
        await live.GotoAsync("about:blank");
    }
}
