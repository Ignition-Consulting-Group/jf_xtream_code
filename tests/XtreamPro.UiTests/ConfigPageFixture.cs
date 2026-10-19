using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using XtreamPro.IntegrationTests.Jellyfin;

namespace XtreamPro.UiTests;

/// <summary>
/// A Jellyfin container with the plugin, a signed-in browser and the plugin page open.
/// </summary>
public sealed class ConfigPageFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public JellyfinFixture Jellyfin { get; } = new();

    public JellyfinApi Api { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await Jellyfin.InitializeAsync();
        Api = new JellyfinApi(Jellyfin.BaseAddress);
        await Api.SetUpAsync(TestContext.Current.CancellationToken);

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            ExecutablePath = Environment.GetEnvironmentVariable("PLAYWRIGHT_CHROMIUM_EXECUTABLE"),
        });
    }

    public const string PluginId = "d7920906-a306-4216-911b-39474ca7596d";

    /// <summary>
    /// Saves one provider pointing at FakeXtreamServer, replacing any others.
    /// </summary>
    /// <returns>A task.</returns>
    public async Task SaveFakeProviderAsync(bool enhancedLiveTv = false)
    {
        var ct = TestContext.Current.CancellationToken;
        var config = await Api.GetAsync($"/Plugins/{PluginId}/Configuration", ct);
        config["Providers"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject
        {
            ["Id"] = "11111111-2222-3333-4444-555555555555",
            ["Name"] = "Fake",
            ["Url"] = JellyfinFixture.FakeProviderUrl,
            ["Username"] = "demo",
            ["Password"] = "demo",
        });
        config["EnableEnhancedLiveTv"] = enhancedLiveTv;
        await Api.PostAsync($"/Plugins/{PluginId}/Configuration", config, ct);
    }

    /// <summary>
    /// Opens the plugin page in a new signed-in browser page.
    /// </summary>
    /// <param name="colorScheme">Preferred colour scheme.</param>
    /// <returns>The page.</returns>
    public async Task<IPage> OpenConfigPageAsync(ColorScheme colorScheme = ColorScheme.Dark)
    {
        var page = await OpenAsync("/web/#/configurationpage?name=Xtream%20Pro", colorScheme);
        await page.GetByRole(AriaRole.Tab, new() { Name = "Providers" }).WaitForAsync();
        return page;
    }

    /// <summary>
    /// Opens a page in a new browser context signed in as the admin user.
    /// </summary>
    /// <param name="path">Path on the Jellyfin server.</param>
    /// <param name="colorScheme">Preferred colour scheme.</param>
    /// <returns>The page.</returns>
    public async Task<IPage> OpenAsync(string path, ColorScheme colorScheme = ColorScheme.Dark)
    {
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { ColorScheme = colorScheme });
        var info = await Api.GetAsync("/System/Info/Public", TestContext.Current.CancellationToken);
        var credentials = JsonSerializer.Serialize(new
        {
            Servers = new[]
            {
                new
                {
                    Id = info["Id"]!.GetValue<string>(),
                    Name = "Test",
                    ManualAddress = Jellyfin.BaseAddress.ToString().TrimEnd('/'),
                    LastConnectionMode = 2,
                    DateLastAccessed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    UserId = Api.UserId,
                    AccessToken = Api.AccessToken,
                },
            },
        });
        await context.AddInitScriptAsync($"window.localStorage.setItem('jellyfin_credentials', {JsonSerializer.Serialize(credentials)});");

        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(Jellyfin.BaseAddress, path).ToString());
        return page;
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
        Api?.Dispose();
        await Jellyfin.DisposeAsync();
    }
}
