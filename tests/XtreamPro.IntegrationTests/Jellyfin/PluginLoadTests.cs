using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace XtreamPro.IntegrationTests.Jellyfin;

[Trait("Category", "Container")]
public class PluginLoadTests : IClassFixture<JellyfinFixture>
{
    private readonly JellyfinFixture _jellyfin;

    public PluginLoadTests(JellyfinFixture jellyfin)
    {
        _jellyfin = jellyfin;
    }

    [Fact]
    public async Task Jellyfin_ServesTheConfigPage()
    {
        using var client = new HttpClient { BaseAddress = _jellyfin.BaseAddress };

        var html = await client.GetStringAsync(
            new Uri("/web/ConfigurationPage?name=Xtream%20Pro", UriKind.Relative),
            TestContext.Current.CancellationToken);

        html.ShouldContain("XtreamProConfigPage");
    }
}
