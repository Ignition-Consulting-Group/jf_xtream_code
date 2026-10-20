using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace XtreamPro.UiTests;

/// <summary>
/// The enhanced Live TV page driven only by a TV remote (arrows, OK, Back, Channel +/-), against a large catalogue.
/// The page is served from the repository and Jellyfin's API is faked, so no server is needed.
/// </summary>
public sealed class TvRemoteTests : IAsyncLifetime
{
    private const string Origin = "http://tv.test";
    private const int ChannelCount = 17000;
    private static readonly string Assets = Path.Combine(RepoRoot(), "src", "Jellyfin.Plugin.XtreamPro", "Web", "Assets");

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage _page = null!;

    public async ValueTask InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            ExecutablePath = Environment.GetEnvironmentVariable("PLAYWRIGHT_CHROMIUM_EXECUTABLE"),
        });
        var context = await _browser.NewContextAsync(new BrowserNewContextOptions { ViewportSize = new ViewportSize { Width = 1920, Height = 1080 } });
        await context.AddInitScriptAsync("localStorage.setItem('jellyfin_credentials', JSON.stringify({ Servers: [{ AccessToken: 't', UserId: 'u' }] }));");
        await context.RouteAsync(Origin + "/**", FulfilAsync);
        _page = await context.NewPageAsync();
        await _page.GotoAsync(Origin + "/XtreamPro/Web/");
        await Assertions.Expect(_page.GetByRole(AriaRole.Status)).ToHaveTextAsync(ChannelCount + " channels");
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
    }

    private static string Category(int i) => "Category " + (i % 40);

    private static async Task FulfilAsync(IRoute route)
    {
        var path = new Uri(route.Request.Url).AbsolutePath;
        if (path == "/XtreamPro/Web/")
        {
            await route.FulfillAsync(new() { Path = Path.Combine(Assets, "index.html"), ContentType = "text/html" });
        }
        else if (path.StartsWith("/XtreamPro/Web/", StringComparison.Ordinal))
        {
            await route.FulfillAsync(new() { Path = Path.Combine(Assets, path["/XtreamPro/Web/".Length..]) });
        }
        else if (path == "/web/")
        {
            await route.FulfillAsync(new() { ContentType = "text/html", Body = "<!DOCTYPE html><title>Jellyfin</title>" });
        }
        else if (path == "/LiveTv/Channels")
        {
            var items = Enumerable.Range(0, ChannelCount).Select(i => new
            {
                Id = "c" + i,
                Name = "Channel " + i,
                ChannelNumber = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                Tags = new[] { Category(i) },
            });
            await route.FulfillAsync(new() { ContentType = "application/json", Body = JsonSerializer.Serialize(new { Items = items }) });
        }
        else if (path.EndsWith("/PlaybackInfo", StringComparison.Ordinal))
        {
            var body = new { PlaySessionId = "ps", MediaSources = new[] { new { LiveStreamId = "ls", TranscodingUrl = "/live.m3u8?api_key=t" } } };
            await route.FulfillAsync(new() { ContentType = "application/json", Body = JsonSerializer.Serialize(body) });
        }
        else if (path == "/LiveTv/Programs")
        {
            await route.FulfillAsync(new() { ContentType = "application/json", Body = "{\"Items\":[]}" });
        }
        else
        {
            await route.FulfillAsync(new() { Status = 204 });
        }
    }

    private Task<string> FocusedAsync() => _page.EvaluateAsync<string>("() => document.activeElement.id || document.activeElement.textContent");

    private Task PressBackAsync() => _page.EvaluateAsync("() => document.activeElement.dispatchEvent(new KeyboardEvent('keydown', { keyCode: 461, bubbles: true }))");

    private ILocator Options => _page.GetByRole(AriaRole.Option);

    [Fact]
    public async Task LargeCatalogue_OnlyRendersTheRowsInView()
    {
        (await Options.CountAsync()).ShouldBeLessThan(60);

        await _page.Locator("#channel-scroll").EvaluateAsync("e => e.scrollTop = e.scrollHeight");

        await Assertions.Expect(_page.GetByRole(AriaRole.Option, new() { Name = "Channel " + (ChannelCount - 1), Exact = false })).ToBeVisibleAsync();
        (await Options.CountAsync()).ShouldBeLessThan(60);
    }

    [Fact]
    public async Task Remote_MovesBetweenCategoriesAndChannelsAndZaps()
    {
        (await FocusedAsync()).ShouldBe("channels");

        await _page.Keyboard.PressAsync("ArrowLeft");
        (await FocusedAsync()).ShouldBe("All");
        await _page.Keyboard.PressAsync("ArrowDown");
        await _page.Keyboard.PressAsync("ArrowDown");
        await _page.Keyboard.PressAsync("ArrowDown");
        (await FocusedAsync()).ShouldBe(Category(0));
        await _page.Keyboard.PressAsync("Enter");
        (await FocusedAsync()).ShouldBe("channels");

        await _page.Keyboard.PressAsync("ArrowDown");
        await Assertions.Expect(_page.Locator("#channel-name")).ToHaveTextAsync("1 Channel 0");
        await _page.Keyboard.PressAsync("ArrowDown");
        await Assertions.Expect(_page.Locator("#channel-name")).ToHaveTextAsync("41 Channel 40");
        await _page.Keyboard.PressAsync("PageDown");
        await Assertions.Expect(_page.Locator("#channel-name")).ToHaveTextAsync("1 Channel 0");
    }

    [Fact]
    public async Task Remote_OkGoesFullScreenAndBackStepsOut()
    {
        await _page.Keyboard.PressAsync("ArrowDown");
        await Assertions.Expect(_page.GetByRole(AriaRole.Status)).ToHaveTextAsync("Playing Channel 0");

        await _page.Keyboard.PressAsync("Enter");
        await _page.WaitForFunctionAsync("() => document.fullscreenElement && document.fullscreenElement.id === 'video'");

        await PressBackAsync();
        await _page.WaitForFunctionAsync("() => !document.fullscreenElement");
        await PressBackAsync();
        (await FocusedAsync()).ShouldBe("All");
        await PressBackAsync();
        await _page.WaitForURLAsync(Origin + "/web/");
    }

    [Fact]
    public async Task Remote_ScrollsDeepIntoALongList()
    {
        for (var i = 0; i < 40; i++)
        {
            await _page.Keyboard.PressAsync("ArrowDown");
        }

        await Assertions.Expect(_page.Locator("#channel-name")).ToHaveTextAsync("40 Channel 39");
        await Assertions.Expect(_page.Locator("#ch-c39")).ToBeInViewportAsync();
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir!.FullName, "Jellyfin.Plugin.XtreamPro.slnx")))
        {
            dir = dir.Parent;
        }

        return dir.FullName;
    }
}
